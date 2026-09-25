namespace MVCCoreApp.Services.Playback;

/// <summary>Opciones del módulo playback (appsettings.json → "Playback").</summary>
public class PlaybackOptions
{
    /// <summary>Proveedor primario: "youtube" (único soportado como primario).</summary>
    public string Primary { get; set; } = "youtube";

    /// <summary>Si true, ante fallo de YouTube se intenta el fallback Saavn.</summary>
    public bool EnableSaavnFallback { get; set; } = true;

    /// <summary>Minutos de caché en memoria de la URL firmada (la URL expira en ~6h).</summary>
    public int CacheMinutes { get; set; } = 300;

    /// <summary>Máximo de resultados de búsqueda por request.</summary>
    public int MaxSearchResults { get; set; } = 10;
}
