using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MVCCoreApp.Dtos;
using MVCCoreApp.Models;
using MVCCoreApp.Services;
using MVCCoreApp.Services.Playback;

namespace MVCCoreApp.Controllers.Api.V1;

/// <summary>
/// Playback interno para ensayo (NO público): resuelve metadatos + URL firmada.
/// El navegador reproduce con &lt;audio&gt; (play/pause/seek/volumen nativos, sin anuncios).
/// Aislado tras IAudioResolver: si la API no oficial se rompe, el CRUD de setlists sigue vivo.
/// </summary>
[ApiController]
[Route("api/v1/playback")]
[Authorize(Policy = "BasicOrCookie")]
public class PlaybackController : ControllerBase
{
    private readonly AudioResolverFactory _factory;
    private readonly MongoDbService _mongo;

    public PlaybackController(AudioResolverFactory factory, MongoDbService mongo)
    {
        _factory = factory;
        _mongo = mongo;
    }

    /// <summary>Busca canciones (YouTube primario, Saavn fallback). Ej: ?q=luna rosa&amp;limit=8</summary>
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<SongHitDto>>> Search([FromQuery] string q, [FromQuery] int limit = 8, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return BadRequest(new { error = "Parámetro 'q' requerido (mín. 2 caracteres)." });
        try
        {
            return Ok(await _factory.SearchAsync(q.Trim(), limit, ct));
        }
        catch (StreamNotAvailableException ex)
        {
            return StatusCode(424, new { error = ex.Message, providerErrors = ex.ProviderErrors });
        }
    }

    /// <summary>
    /// Resuelve UNA pista a URL reproducible. Acepta videoId, youtubeUrl o texto libre (?q=).
    /// La URL expira (~5h caché): el front la pide al pulsar play, nunca la guarda.
    /// </summary>
    [HttpGet("resolve")]
    public async Task<ActionResult<ResolvedAudioDto>> Resolve(
        [FromQuery] string? videoId, [FromQuery] string? youtubeUrl, [FromQuery] string? q,
        CancellationToken ct = default)
    {
        var input = (videoId ?? youtubeUrl ?? q)?.Trim();
        if (string.IsNullOrWhiteSpace(input))
            return BadRequest(new { error = "Pasa 'videoId', 'youtubeUrl' o 'q'." });
        try
        {
            return Ok(await _factory.ResolveAsync(input, ct));
        }
        catch (StreamNotAvailableException ex)
        {
            return StatusCode(424, new { error = ex.Message, providerErrors = ex.ProviderErrors });
        }
    }

    /// <summary>
    /// Hidrata un setlist para el player: por cada item indica videoId si el catálogo
    /// Track.Links.YouTubeUrl lo tiene; si no, el front buscará por título (resolve?q=).
    /// No devuelve StreamUrls para no quemar la expiración.
    /// </summary>
    [HttpGet("setlist/{id}")]
    public async Task<ActionResult<PlayableSetlistDto>> GetPlayableSetlist(string id, CancellationToken ct = default)
    {
        var setlist = await _mongo.GetCollection<Setlist>("setlists")
            .Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync(ct);
        if (setlist == null) return NotFound(new { error = "Setlist no encontrado." });

        var trackIds = setlist.Items.Where(i => i.TrackId != null).Select(i => i.TrackId!).Distinct().ToList();
        var tracks = trackIds.Count == 0
            ? new List<Track>()
            : await _mongo.GetCollection<Track>("tracks")
                .Find(x => trackIds.Contains(x.Id) && !x.IsDeleted).ToListAsync(ct);
        var byId = tracks.ToDictionary(t => t.Id, t => t);

        var items = setlist.Items.Select((it, idx) =>
        {
            string? videoId = null, artist = null, cover = null;
            if (it.TrackId != null && byId.TryGetValue(it.TrackId, out var t))
                videoId = YouTubeIdParser.TryExtract(t.Links?.YouTubeUrl);
            return new PlayableSetlistItemDto(
                idx + 1, it.Source, it.TrackId, it.Title, videoId, artist, cover,
                Resolvable: videoId != null || !string.IsNullOrWhiteSpace(it.Title));
        }).ToList();

        return Ok(new PlayableSetlistDto(setlist.Id, setlist.Name, items));
    }
}
