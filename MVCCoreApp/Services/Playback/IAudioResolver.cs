using MVCCoreApp.Dtos;

namespace MVCCoreApp.Services.Playback;

/// <summary>
/// Contrato del módulo playback. Toda API no oficial vive tras esta interfaz:
/// si YouTube/Saavn cambian, solo se rompe /api/v1/playback/*, nunca el CRUD de setlists.
/// </summary>
public interface IAudioResolver
{
    string ProviderName { get; }
    Task<IReadOnlyList<SongHitDto>> SearchAsync(string query, int limit, CancellationToken ct);
    Task<ResolvedAudioDto> ResolveByIdAsync(string id, CancellationToken ct);
}
