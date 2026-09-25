#pragma warning disable SYSLIB0021 // DES es legacy pero JioSaavn lo exige (igual que LibSaavn)
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MVCCoreApp.Dtos;

namespace MVCCoreApp.Services.Playback;

/// <summary>
/// Fallback secundario: JioSaavn vía HttpClient directo (sin paquete LibSaavn).
/// Evaluación: LibSaavn 1.0.0 funciona (expone DecryptedMediaUrl) pero está abandonado y
/// arrastra Microsoft.AspNetCore.Components.WebAssembly 5.0.7 al servidor: deuda pesada.
/// Esta implementación replica su descifrado DES-ECB (key "38346591") con ~80 líneas propias,
/// sin dependencias extra. Catálogo sesgado a India: solo fallback, nunca primario.
/// </summary>
public sealed class SaavnFallbackResolver : IAudioResolver
{
    public string ProviderName => "saavn";

    private static readonly byte[] DesKey = Encoding.UTF8.GetBytes("38346591");
    private const string Base = "https://www.jiosaavn.com/api.php";

    private readonly IHttpClientFactory _http;
    private readonly IMemoryCache _cache;
    private readonly PlaybackOptions _opt;
    private readonly ILogger<SaavnFallbackResolver> _logger;

    public SaavnFallbackResolver(
        IHttpClientFactory http,
        IMemoryCache cache,
        IOptions<PlaybackOptions> opt,
        ILogger<SaavnFallbackResolver> logger)
    {
        _http = http;
        _cache = cache;
        _opt = opt.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SongHitDto>> SearchAsync(string query, int limit, CancellationToken ct)
    {
        query = query.Trim();
        if (query.Length < 2) return Array.Empty<SongHitDto>();
        limit = Math.Clamp(limit, 1, Math.Min(_opt.MaxSearchResults, 25));

        try
        {
            var client = Client();
            var url = $"{Base}?__call=search.getResults&q={Uri.EscapeDataString(query)}&p=1&n={limit}" +
                      "&_format=json&_marker=0&api_version=4&ctx=web6dot0";
            var res = await client.GetFromJsonAsync<SaavnSearchResponse>(url, ct);
            if (res?.Results == null) return Array.Empty<SongHitDto>();

            return res.Results.Take(limit).Select(s => new SongHitDto(
                "saavn:" + s.Id,
                HtmlDecode(s.Title) ?? "(sin título)",
                HtmlDecode(FirstArtist(s.Subtitle)),
                int.TryParse(s.MoreInfo?.Duration, out var d) ? d : null,
                UpgradeImage(s.Image),
                ProviderName)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Búsqueda Saavn falló para {Query}", query);
            throw new StreamNotAvailableException($"Búsqueda Saavn falló: {ex.Message}", ProviderName);
        }
    }

    public async Task<ResolvedAudioDto> ResolveByIdAsync(string id, CancellationToken ct)
    {
        var songId = id.StartsWith("saavn:", StringComparison.OrdinalIgnoreCase) ? id[6..] : id.Trim();
        if (string.IsNullOrWhiteSpace(songId))
            throw new StreamNotAvailableException("Id Saavn vacío", ProviderName);

        var cacheKey = $"pb:saavn:{songId}";
        if (_cache.TryGetValue<ResolvedAudioDto>(cacheKey, out var cached) && cached != null)
            return cached;

        try
        {
            var client = Client();
            var url = $"{Base}?__call=song.getDetails&pids={Uri.EscapeDataString(songId)}" +
                      "&_format=json&_marker=0&api_version=4&ctx=web6dot0";
            var res = await client.GetFromJsonAsync<SaavnDetailsResponse>(url, ct);
            var song = res?.Songs?.FirstOrDefault()
                ?? throw new StreamNotAvailableException($"Canción Saavn no encontrada ({songId})", ProviderName);

            var enc = song.MoreInfo?.EncryptedMediaUrl;
            if (string.IsNullOrWhiteSpace(enc))
                throw new StreamNotAvailableException($"Saavn sin media cifrada ({songId})", ProviderName);

            var low = DecryptMediaUrl(enc); // …_96.mp4
            var hiFi = song.MoreInfo?.Has320 == "true";
            // Subir calidad si el track lo permite (_96 → _160/_320), igual que LibSaavn.
            var streamUrl = hiFi ? low.Replace("_96.", "_320.") : low.Replace("_96.", "_160.");

            var ttl = TimeSpan.FromMinutes(Math.Clamp(_opt.CacheMinutes, 30, 360));
            var dto = new ResolvedAudioDto(
                "saavn:" + song.Id,
                HtmlDecode(song.Title) ?? songId,
                HtmlDecode(FirstArtist(song.Subtitle)),
                int.TryParse(song.MoreInfo?.Duration, out var d) ? d : null,
                UpgradeImage(song.Image),
                streamUrl,
                DateTimeOffset.UtcNow.Add(ttl),
                ProviderName,
                hiFi ? "320k" : "160k");
            _cache.Set(cacheKey, dto, ttl);
            return dto;
        }
        catch (StreamNotAvailableException) { throw; }
        catch (Exception ex)
        {
            throw new StreamNotAvailableException($"Resolve Saavn falló ({songId}): {ex.Message}", ProviderName);
        }
    }

    // --- helpers ---

    private HttpClient Client()
    {
        var c = _http.CreateClient("Saavn");
        if (c.DefaultRequestHeaders.UserAgent.Count == 0)
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36");
        return c;
    }

    internal static string DecryptMediaUrl(string encrypted)
    {
        var raw = Convert.FromBase64String(encrypted.Trim());
        using var des = DES.Create();
        des.Key = DesKey;
        des.Mode = CipherMode.ECB;
        des.Padding = PaddingMode.PKCS7;
        using var dec = des.CreateDecryptor();
        var plain = dec.TransformFinalBlock(raw, 0, raw.Length);
        return Encoding.UTF8.GetString(plain).Trim();
    }

    private static string? HtmlDecode(string? s) =>
        string.IsNullOrWhiteSpace(s) ? s : System.Net.WebUtility.HtmlDecode(s.Trim(' ', '"'));

    private static string? FirstArtist(string? subtitle)
    {
        if (string.IsNullOrWhiteSpace(subtitle)) return null;
        var dash = subtitle.IndexOf(" - ", StringComparison.Ordinal);
        return (dash > 0 ? subtitle[..dash] : subtitle).Trim();
    }

    private static string? UpgradeImage(string? url) =>
        string.IsNullOrWhiteSpace(url) ? url : url.Replace("150x150", "500x500").Replace("50x50", "500x500");

    // JSON mínimo de JioSaavn (solo campos usados)
    private sealed class SaavnSearchResponse
    {
        [JsonPropertyName("results")] public List<SaavnSong>? Results { get; set; }
    }
    private sealed class SaavnDetailsResponse
    {
        [JsonPropertyName("songs")] public List<SaavnSong>? Songs { get; set; }
    }
    private sealed class SaavnSong
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("subtitle")] public string? Subtitle { get; set; }
        [JsonPropertyName("image")] public string? Image { get; set; }
        [JsonPropertyName("more_info")] public SaavnMore? MoreInfo { get; set; }
    }
    private sealed class SaavnMore
    {
        [JsonPropertyName("encrypted_media_url")] public string? EncryptedMediaUrl { get; set; }
        [JsonPropertyName("duration")] public string? Duration { get; set; }
        [JsonPropertyName("320kbps")] public string? Has320 { get; set; }
    }
}
