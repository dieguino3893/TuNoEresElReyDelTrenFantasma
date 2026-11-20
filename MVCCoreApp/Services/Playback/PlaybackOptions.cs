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

    /// <summary>
    /// Proxy HTTP para resolver audio de YouTube desde una IP no bloqueada.
    /// Formato: http://usuario:clave@host:puerto. Vacío = sin proxy.
    /// </summary>
    public string? ProxyUrl { get; set; }

    /// <summary>
    /// Header de cookies de una sesión de YouTube ("SID=...; HSID=...; ...").
    /// Evita el bot-check desde IPs de datacenter. Vacío = sin cookies.
    /// </summary>
    public string? YouTubeCookies { get; set; }

    /// <summary>
    /// URL de un resolver externo (ej. Google Apps Script) que devuelve
    /// {url,title,author,duration}. Se consulta antes que YoutubeExplode.
    /// Vacío = desactivado.
    /// </summary>
    public string? ResolverUrl { get; set; }

    /// <summary>Token opcional para el resolver externo (header Authorization: Bearer).</summary>
    public string? ResolverKey { get; set; }

    /// <summary>
    /// Llave del link público de ensayo (/ensayo/play/{id}?key=... y ?key= en la API de playback).
    /// Vacía = sin acceso compartido.
    /// </summary>
    public string? ShareKey { get; set; }

    /// <summary>
    /// Cookie ARL de una cuenta Deezer. Si está puesta, el resolve por texto cae a Deezer
    /// (audio completo, sin anuncios) cuando YouTube falla.
    /// </summary>
    public string? DeezerArl { get; set; }
}
