using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MVCCoreApp.Dtos;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace MVCCoreApp.Services.Playback;

/// <summary>
/// Stream de Deezer con cookie ARL (cuenta propia). El audio llega cifrado (Blowfish CBC)
/// y se descifra a un archivo temporal que se sirve con soporte de rangos.
/// </summary>
public sealed class DeezerArlClient
{
    private const string Api = "https://www.deezer.com/ajax/gw-light.php";
    private const string MediaApi = "https://media.deezer.com/v1/get_url";
    private const string PublicApi = "https://api.deezer.com";
    private const string UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    private readonly IHttpClientFactory _http;
    private readonly IMemoryCache _cache;
    private readonly PlaybackOptions _opt;
    private readonly ILogger<DeezerArlClient> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public bool Enabled => !string.IsNullOrWhiteSpace(_opt.DeezerArl);

    public DeezerArlClient(IHttpClientFactory http, IMemoryCache cache, IOptions<PlaybackOptions> opt, ILogger<DeezerArlClient> logger)
    {
        _http = http;
        _cache = cache;
        _opt = opt.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SongHitDto>> SearchAsync(string query, int limit, CancellationToken ct)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(query)) return Array.Empty<SongHitDto>();
        query = query.Trim();
        limit = Math.Clamp(limit, 1, 10);
        try
        {
            var http = Client();
            var url = $"{PublicApi}/search?q={Uri.EscapeDataString(query)}&limit={limit}";
            using var doc = JsonDocument.Parse(await http.GetStringAsync(url, ct));
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return Array.Empty<SongHitDto>();

            var hits = new List<SongHitDto>();
            foreach (var t in data.EnumerateArray())
            {
                var id = t.TryGetProperty("id", out var i) ? i.GetInt64().ToString() : null;
                var title = t.TryGetProperty("title", out var ti) ? ti.GetString() : null;
                if (id == null || string.IsNullOrWhiteSpace(title)) continue;
                var artist = t.TryGetProperty("artist", out var ar) && ar.TryGetProperty("name", out var an) ? an.GetString() : null;
                var duration = t.TryGetProperty("duration", out var d) ? d.GetInt32() : (int?)null;
                var cover = t.TryGetProperty("album", out var al) && al.TryGetProperty("cover_medium", out var cm) ? cm.GetString() : null;
                hits.Add(new SongHitDto(id, title!, artist, duration, cover, "deezer"));
            }

            // Preferir coincidencia exacta de título; evita covers/versiones raras.
            var exact = hits.FirstOrDefault(h => string.Equals(Normalize(h.Title), Normalize(query), StringComparison.Ordinal));
            return exact != null ? new[] { exact }.Concat(hits.Where(h => h != exact)).ToList() : hits;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Búsqueda Deezer falló para {Query}", query);
            return Array.Empty<SongHitDto>();
        }
    }

    /// <summary>Resuelve metadatos + URL cifrada del track.</summary>
    public async Task<DeezerTrack?> ResolveAsync(string trackId, CancellationToken ct)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(trackId)) return null;

        var session = await GetSessionAsync(ct);
        var http = Client(BrowserHeaders());

        var trackJson = await GwCallAsync(http, "song.getData", session.ApiToken,
            new Dictionary<string, object> { ["SNG_ID"] = trackId }, ct);
        if (trackJson == null) return null;

        var root = trackJson.Value;
        var md5 = root.TryGetProperty("MD5_ORIGIN", out var m) ? m.GetString() : null;
        var mediaVersion = root.TryGetProperty("MEDIA_VERSION", out var mv) ? mv.GetString() : null;
        var trackToken = root.TryGetProperty("TRACK_TOKEN", out var tt) ? tt.GetString() : null;
        if (string.IsNullOrWhiteSpace(trackToken)) return null;

        using var req = new HttpRequestMessage(HttpMethod.Post, MediaApi);
        req.Headers.TryAddWithoutValidation("Cookie", $"arl={_opt.DeezerArl}");
        req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        req.Content = new StringContent(JsonSerializer.Serialize(new
        {
            license_token = session.LicenseToken,
            media = new[] { new { type = "FULL", formats = new[] { new { cipher = "BF_CBC_STRIPE", format = "MP3_128" } } } },
            track_tokens = new[] { trackToken }
        }), Encoding.UTF8, "application/json");

        using var mediaRes = await http.SendAsync(req, ct);
        if (!mediaRes.IsSuccessStatusCode) return null;
        using var mediaDoc = JsonDocument.Parse(await mediaRes.Content.ReadAsStringAsync(ct));
        var streamUrl = ExtractStreamUrl(mediaDoc.RootElement);
        if (string.IsNullOrWhiteSpace(streamUrl)) return null;

        return new DeezerTrack(
            trackId,
            root.TryGetProperty("SNG_TITLE", out var t) ? t.GetString() ?? trackId : trackId,
            root.TryGetProperty("ART_NAME", out var a) ? a.GetString() : null,
            root.TryGetProperty("DURATION", out var du) ? du.GetInt32() : null,
            root.TryGetProperty("ALB_PICTURE", out var pic) && pic.GetString() is { Length: > 0 } p
                ? $"https://e-cdns-images.dzcdn.net/images/cover/{p}/250x250-000000-80-0-0.jpg"
                : null,
            streamUrl,
            md5,
            mediaVersion);
    }

    /// <summary>Descarga+descifra a un archivo temporal (cacheado ~45 min).</summary>
    public async Task<string?> GetDecryptedFileAsync(string trackId, CancellationToken ct)
    {
        if (!Enabled) return null;
        var dir = Path.Combine(Path.GetTempPath(), "elrey-deezer");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{trackId}.mp3");

        if (File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < TimeSpan.FromMinutes(45))
            return path;

        var gate = _locks.GetOrAdd(trackId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < TimeSpan.FromMinutes(45))
                return path;

            var track = await ResolveAsync(trackId, ct);
            if (track == null) return null;

            var temp = path + ".tmp";
            using (var http = Client(BrowserHeaders()))
            using (var res = await http.GetAsync(track.Url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                if (!res.IsSuccessStatusCode) return null;
                await using var src = await res.Content.ReadAsStreamAsync(ct);
                await using var dst = File.Create(temp);
                await DecryptAsync(src, dst, BlowfishKey(trackId), ct);
            }

            File.Move(temp, path, true);
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Deezer decrypt falló para {TrackId}", trackId);
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<Session> GetSessionAsync(CancellationToken ct)
    {
        const string key = "pb:deezer:session";
        if (_cache.TryGetValue<Session>(key, out var cached) && cached != null) return cached;

        var http = Client(BrowserHeaders());
        var doc = await GwCallAsync(http, "deezer.getUserData", "null", new Dictionary<string, object>(), ct)
            ?? throw new StreamNotAvailableException("Deezer: sin sesión (ARL inválido)", "deezer");

        var user = doc.TryGetProperty("USER", out var u) ? u : default;
        var userId = user.ValueKind == JsonValueKind.Object && user.TryGetProperty("USER_ID", out var uid) ? uid.GetInt64() : 0;
        if (userId <= 0) throw new StreamNotAvailableException("Deezer: ARL inválido o vencido", "deezer");

        var options = user.TryGetProperty("OPTIONS", out var o) ? o : default;
        var license = options.ValueKind == JsonValueKind.Object && options.TryGetProperty("license_token", out var lt) ? lt.GetString() : null;
        var apiToken = doc.TryGetProperty("checkForm", out var cf) ? cf.GetString() : null;
        if (string.IsNullOrWhiteSpace(license) || string.IsNullOrWhiteSpace(apiToken))
            throw new StreamNotAvailableException("Deezer: sesión sin license_token", "deezer");

        var session = new Session(apiToken!, license!);
        _cache.Set(key, session, TimeSpan.FromMinutes(30));
        return session;
    }

    private async Task<JsonElement?> GwCallAsync(HttpClient http, string method, string apiToken, Dictionary<string, object> args, CancellationToken ct)
    {
        var url = $"{Api}?method={method}&input=3&api_version=1.0&api_token={Uri.EscapeDataString(apiToken)}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.TryAddWithoutValidation("Cookie", $"arl={_opt.DeezerArl}");
        req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        req.Content = new StringContent(JsonSerializer.Serialize(args), Encoding.UTF8, "application/json");

        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));

        if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Array && err.GetArrayLength() > 0)
            return null;
        if (doc.RootElement.TryGetProperty("error", out err) && err.ValueKind == JsonValueKind.Object && err.EnumerateObject().Any())
            return null;

        return doc.RootElement.TryGetProperty("results", out var results) ? results.Clone() : null;
    }

    private static string? ExtractStreamUrl(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return null;
        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("media", out var media) || media.ValueKind != JsonValueKind.Array) continue;
            foreach (var m in media.EnumerateArray())
            {
                if (m.TryGetProperty("sources", out var sources) && sources.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in sources.EnumerateArray())
                        if (s.TryGetProperty("url", out var u) && u.GetString() is { Length: > 0 } url)
                            return url;
                }
            }
        }
        return null;
    }

    private static async Task DecryptAsync(Stream src, Stream dst, byte[] key, CancellationToken ct)
    {
        const int block = 2048;
        const int group = block * 3;
        var buffer = new byte[group * 2];
        var filled = 0;
        int read;
        while ((read = await src.ReadAsync(buffer.AsMemory(filled), ct)) > 0)
        {
            filled += read;
            while (filled >= group)
            {
                await WriteGroupAsync(dst, buffer, group, key, ct);
                Buffer.BlockCopy(buffer, group, buffer, 0, filled - group);
                filled -= group;
            }
        }
        if (filled > 0)
        {
            if (filled >= block)
            {
                var decrypted = DecryptBlock(buffer.AsSpan(0, block).ToArray(), key);
                await dst.WriteAsync(decrypted, ct);
                await dst.WriteAsync(buffer.AsMemory(block, filled - block), ct);
            }
            else
            {
                await dst.WriteAsync(buffer.AsMemory(0, filled), ct);
            }
        }
    }

    private static async Task WriteGroupAsync(Stream dst, byte[] buffer, int group, byte[] key, CancellationToken ct)
    {
        var decrypted = DecryptBlock(buffer.AsSpan(0, 2048).ToArray(), key);
        await dst.WriteAsync(decrypted, ct);
        await dst.WriteAsync(buffer.AsMemory(2048, group - 2048), ct);
    }

    private static byte[] DecryptBlock(byte[] input, byte[] key)
    {
        var cipher = new BufferedBlockCipher(new CbcBlockCipher(new BlowfishEngine()));
        cipher.Init(false, new ParametersWithIV(new KeyParameter(key), new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }));
        var output = new byte[cipher.GetOutputSize(input.Length)];
        var len = cipher.ProcessBytes(input, 0, input.Length, output, 0);
        len += cipher.DoFinal(output, len);
        return output[..len];
    }

    private static byte[] BlowfishKey(string trackId)
    {
        using var md5 = MD5.Create();
        var hash = Convert.ToHexString(md5.ComputeHash(Encoding.ASCII.GetBytes(trackId))).ToLowerInvariant();
        const string secret = "g4el58wc0zvf9na1";
        var key = new byte[16];
        for (var i = 0; i < 16; i++)
            key[i] = (byte)(hash[i] ^ hash[i + 16] ^ secret[i]);
        return key;
    }

    private HttpClient Client(Dictionary<string, string>? headers = null)
    {
        var c = _http.CreateClient("Deezer");
        c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
        c.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        return c;
    }

    private static Dictionary<string, string> BrowserHeaders() => new();

    private static string Normalize(string s) =>
        new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private sealed record Session(string ApiToken, string LicenseToken);

    public sealed record DeezerTrack(
        string Id, string Title, string? Artist, int? Duration, string? Cover,
        string Url, string? Md5, string? MediaVersion);
}
