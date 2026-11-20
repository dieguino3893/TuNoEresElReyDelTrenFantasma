using Microsoft.Extensions.Options;
using MVCCoreApp.Dtos;

namespace MVCCoreApp.Services.Playback;

/// <summary>
/// Orquesta primario → fallback. El controller solo habla con el factory:
/// si YouTube cae, se intenta Saavn; si ambos caen, 424 con providerErrors[].
/// Kill-switch: Playback:EnableSaavnFallback=false desactiva el fallback sin desplegar.
/// </summary>
public sealed class AudioResolverFactory
{
    private readonly YoutubeExplodeResolver _youtube;
    private readonly SaavnFallbackResolver _saavn;
    private readonly DeezerArlClient _deezer;
    private readonly PlaybackOptions _opt;
    private readonly ILogger<AudioResolverFactory> _logger;

    public AudioResolverFactory(
        YoutubeExplodeResolver youtube,
        SaavnFallbackResolver saavn,
        DeezerArlClient deezer,
        IOptions<PlaybackOptions> opt,
        ILogger<AudioResolverFactory> logger)
    {
        _youtube = youtube;
        _saavn = saavn;
        _deezer = deezer;
        _opt = opt.Value;
        _logger = logger;
    }

    private IEnumerable<IAudioResolver> Chain()
    {
        yield return _youtube; // primario fijo: mejor mantenimiento y catálogo occidental
        if (_opt.EnableSaavnFallback) yield return _saavn;
    }

    public async Task<IReadOnlyList<SongHitDto>> SearchAsync(string query, int limit, CancellationToken ct)
    {
        var errors = new List<string>();
        foreach (var r in Chain())
        {
            try { return await r.SearchAsync(query, limit, ct); }
            catch (StreamNotAvailableException ex)
            {
                errors.Add($"{r.ProviderName}: {ex.Message}");
                _logger.LogWarning(ex, "Search falló en {Provider}, probando siguiente", r.ProviderName);
            }
        }
        throw new StreamNotAvailableException("Búsqueda no disponible en ningún proveedor", null, errors);
    }

    public async Task<ResolvedAudioDto> ResolveAsync(string idOrUrl, CancellationToken ct)
    {
        // Ids Saavn van directos al fallback (evita un round-trip a YouTube).
        if (idOrUrl.StartsWith("saavn:", StringComparison.OrdinalIgnoreCase))
            return await _saavn.ResolveByIdAsync(idOrUrl, ct);

        var errors = new List<string>();
        var videoId = YouTubeIdParser.TryExtract(idOrUrl);

        if (videoId != null)
        {
            try { return await _youtube.ResolveByIdAsync(videoId, ct); }
            catch (StreamNotAvailableException ex)
            {
                errors.Add($"youtube: {ex.Message}");
                _logger.LogWarning(ex, "Resolve YouTube falló para {Id}", videoId);
            }
            // Un videoId de YouTube no tiene sentido en Saavn: no fallback por id.
            throw new StreamNotAvailableException($"Audio no disponible para {videoId}", "youtube", errors);
        }

        // Texto libre: buscar en YouTube y resolver el primer hit (flujo "título del setlist → audio").
        SongHitDto? first = null;
        try
        {
            first = (await SearchAsync(idOrUrl, 1, ct)).FirstOrDefault();
        }
        catch (StreamNotAvailableException ex)
        {
            errors.Add(ex.Message);
        }

        if (first != null)
        {
            try
            {
                return await ResolveAsync(first.VideoId, ct);
            }
            catch (StreamNotAvailableException ex)
            {
                errors.Add(ex.Message);
                // El videoId sí existe (la búsqueda funcionó): el cliente puede reproducirlo en embed.
                if (!_deezer.Enabled)
                    throw new StreamNotAvailableException(ex.Message, ex.Provider, ex.ProviderErrors, first.VideoId);
            }
        }

        // Deezer con ARL: audio completo sin anuncios cuando YouTube no entrega stream.
        if (_deezer.Enabled)
        {
            try
            {
                var dh = await _deezer.SearchAsync(idOrUrl, 3, ct);
                var d = dh.FirstOrDefault();
                if (d != null)
                {
                    return new ResolvedAudioDto(
                        d.VideoId, d.Title, d.Artist, d.DurationSeconds, d.CoverUrl,
                        $"/api/v1/playback/deezer/stream?trackId={d.VideoId}",
                        DateTimeOffset.UtcNow.AddMinutes(45), "deezer", "mp3");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"deezer: {ex.Message}");
            }
        }

        throw new StreamNotAvailableException(
            $"Audio no disponible para '{idOrUrl}'", null, errors, first?.VideoId);
    }
}
