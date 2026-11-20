using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MVCCoreApp.Dtos;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos.Streams;

namespace MVCCoreApp.Services.Playback;

/// <summary>
/// Proveedor primario: YouTube vía YoutubeExplode (NuGet, sin API key, sin cuota).
/// Evaluación: recomendado sobre YouTubeApi/InnerTube (archivado) por mantenimiento y calidad.
/// Solo este archivo referencia YoutubeExplode: aislamiento ante cambios de la API interna.
/// </summary>
public sealed class YoutubeExplodeResolver : IAudioResolver
{
    public string ProviderName => "youtube";

    private readonly YoutubeClient _client;
    private readonly IMemoryCache _cache;
    private readonly PlaybackOptions _opt;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<YoutubeExplodeResolver> _logger;

    public YoutubeExplodeResolver(
        YoutubeClient client,
        IMemoryCache cache,
        IOptions<PlaybackOptions> opt,
        IHttpClientFactory http,
        ILogger<YoutubeExplodeResolver> logger)
    {
        _client = client;
        _cache = cache;
        _opt = opt.Value;
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SongHitDto>> SearchAsync(string query, int limit, CancellationToken ct)
    {
        query = query.Trim();
        if (query.Length < 2) return Array.Empty<SongHitDto>();
        limit = Math.Clamp(limit, 1, Math.Min(_opt.MaxSearchResults, 25));

        var hits = new List<SongHitDto>();
        try
        {
            await foreach (var r in _client.Search.GetVideosAsync(query, ct))
            {
                hits.Add(new SongHitDto(
                    r.Id.Value,
                    r.Title,
                    r.Author?.ChannelTitle,
                    r.Duration is TimeSpan d ? (int)d.TotalSeconds : null,
                    r.Thumbnails.TryGetWithHighestResolution()?.Url,
                    ProviderName));
                if (hits.Count >= limit) break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Búsqueda YouTube falló para {Query}", query);
            throw new StreamNotAvailableException($"Búsqueda YouTube falló: {ex.Message}", ProviderName);
        }
        return hits;
    }

    public async Task<ResolvedAudioDto> ResolveByIdAsync(string id, CancellationToken ct)
    {
        var videoId = YouTubeIdParser.TryExtract(id) ?? id.Trim();
        var cacheKey = $"pb:yt:{videoId}";

        // La URL firmada (googlevideo …?expire=…) caduca: caché corta, NUNCA persistir en Mongo.
        if (_cache.TryGetValue<ResolvedAudioDto>(cacheKey, out var cached) && cached != null)
            return cached;

        // Resolver externo (Apps Script en IPs de Google): evita el bot-check del datacenter.
        if (!string.IsNullOrWhiteSpace(_opt.ResolverUrl))
        {
            var remoto = await TryRemoteAsync(videoId, cacheKey, ct);
            if (remoto != null) return remoto;
        }

        try
        {
            // Reintentos solo para errores transitorios de red (2 intentos, backoff simple).
            Exception? last = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    return await ResolveUncachedAsync(videoId, cacheKey, ct);
                }
                catch (HttpRequestException ex) when (attempt == 0)
                {
                    last = ex;
                    _logger.LogWarning(ex, "Red transitoria resolviendo {VideoId}, reintento", videoId);
                    await Task.Delay(600, ct);
                }
            }
            throw last ?? new StreamNotAvailableException($"No se pudo resolver {videoId}", ProviderName);
        }
        catch (VideoUnavailableException ex)
        {
            throw new StreamNotAvailableException($"Video no disponible ({videoId}): {ex.Message}", ProviderName);
        }
        catch (VideoUnplayableException ex)
        {
            throw new StreamNotAvailableException($"Video no reproducible ({videoId}): {ex.Message}", ProviderName);
        }
    }

    private async Task<ResolvedAudioDto?> TryRemoteAsync(string videoId, string cacheKey, CancellationToken ct)
    {
        try
        {
            var cliente = _http.CreateClient("PlaybackRemote");
            var sep = _opt.ResolverUrl!.Contains('?') ? '&' : '?';
            var url = $"{_opt.ResolverUrl}{sep}id={Uri.EscapeDataString(videoId)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(_opt.ResolverKey))
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _opt.ResolverKey);

            using var res = await cliente.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Resolver externo devolvió {Status} para {VideoId}", (int)res.StatusCode, videoId);
                return null;
            }

            var data = await res.Content.ReadFromJsonAsync<RemoteAudio>(cancellationToken: ct);
            if (string.IsNullOrWhiteSpace(data?.Url))
            {
                _logger.LogWarning("Resolver externo sin URL para {VideoId}: {Error}", videoId, data?.Error);
                return null;
            }

            var ttl = TimeSpan.FromMinutes(Math.Clamp(_opt.CacheMinutes, 30, 360));
            var dto = new ResolvedAudioDto(
                videoId,
                data.Title ?? videoId,
                data.Author,
                data.Duration,
                null,
                data.Url,
                DateTimeOffset.UtcNow.Add(ttl),
                "remote",
                "mp3");
            _cache.Set(cacheKey, dto, ttl);
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resolver externo falló para {VideoId}", videoId);
            return null;
        }
    }

    private sealed class RemoteAudio
    {
        public string? Url { get; set; }
        public string? Title { get; set; }
        public string? Author { get; set; }
        public int? Duration { get; set; }
        public string? Error { get; set; }
    }

    private async Task<ResolvedAudioDto> ResolveUncachedAsync(string videoId, string cacheKey, CancellationToken ct)
    {
        var video = await _client.Videos.GetAsync(videoId, ct);
        var manifest = await _client.Videos.Streams.GetManifestAsync(videoId, ct);

        // Audio-only = sin anuncios de video, menor peso, ideal para ensayo.
        var best = manifest.GetAudioOnlyStreams().GetWithHighestBitrate()
            ?? throw new StreamNotAvailableException($"Sin streams de audio para {videoId}", ProviderName);

        var ttl = TimeSpan.FromMinutes(Math.Clamp(_opt.CacheMinutes, 30, 360));
        var dto = new ResolvedAudioDto(
            videoId,
            video.Title,
            video.Author?.ChannelTitle,
            video.Duration is TimeSpan d ? (int)d.TotalSeconds : null,
            video.Thumbnails.TryGetWithHighestResolution()?.Url,
            best.Url,
            DateTimeOffset.UtcNow.Add(ttl),
            ProviderName,
            best.Container.Name);

        _cache.Set(cacheKey, dto, ttl);
        return dto;
    }
}
