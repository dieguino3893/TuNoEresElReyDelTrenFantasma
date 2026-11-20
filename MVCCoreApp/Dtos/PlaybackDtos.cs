namespace MVCCoreApp.Dtos;

// DTOs del módulo playback (uso interno / ensayo).
// El servidor solo resuelve metadatos + URL firmada; el <audio> del navegador reproduce.

/// <summary>Resultado de búsqueda (sin URL de stream, solo identificadores).</summary>
public record SongHitDto(
    string VideoId,
    string Title,
    string? Artist,
    int? DurationSeconds,
    string? CoverUrl,
    string Provider);

/// <summary>Audio resuelto y listo para <audio src="StreamUrl">. StreamUrl expira: no persistir en Mongo.</summary>
public record ResolvedAudioDto(
    string VideoId,
    string Title,
    string? Artist,
    int? DurationSeconds,
    string? CoverUrl,
    string StreamUrl,
    DateTimeOffset ExpiresAt,
    string Provider,
    string? Container);

/// <summary>Item de un setlist enriquecido para el player de ensayo.</summary>
public record PlayableSetlistItemDto(
    int Position,
    string Source,
    string? TrackId,
    string Title,
    string? VideoId,
    string? Artist,
    string? CoverUrl,
    bool Resolvable);

/// <summary>Setlist listo para el player: NO incluye StreamUrls (se resuelven una por una).</summary>
public record PlayableSetlistDto(
    string SetlistId,
    string Name,
    IReadOnlyList<PlayableSetlistItemDto> Items);
