# Módulo Playback — ensayo interno (API resuelve, `<audio>` suena)

Uso privado de la banda. NO exponer en el sitio público: ver §5 Riesgos.

## Qué hace

- `GET /api/v1/playback/search?q=…&limit=8` → `[{videoId,title,artist,durationSeconds,coverUrl,provider}]`
- `GET /api/v1/playback/resolve?videoId=… | ?youtubeUrl=… | ?q=…` → `{…, streamUrl, expiresAt, provider}`
- `GET /api/v1/playback/setlist/{id}` → setlist hidratado con `videoId` por item (sin URLs, para no quemar la expiración)
- Vista `/admin/setlists/play/{id}` → player `<audio>` con play/pause/anterior/siguiente/seek/volumen

Auth: `BasicOrCookie` (sesión admin, Basic o ApiKey). Sin auth → 401.

## Evaluación de paquetes (2026-09)

| Opción | Decisión | Motivo |
|---|---|---|
| **YoutubeExplode 6.6.2** | ✅ primario | Mantenido, sin key/cuota, `audio-only` sin anuncios. Único NuGet nuevo del módulo. |
| **YouTubeApi genérico** | ❌ | Nombre ambiguo, abandonados. Nada que aporte sobre YoutubeExplode. |
| **InnerTube (lighttube-org)** | ❌ | Archivado ("will likely break"). Requiere cookies/token. |
| **LibSaavn 1.0.0** | ❌ como paquete | Funciona (`DecryptedMediaUrl`) pero abandonado y arrastra `Microsoft.AspNetCore.Components.WebAssembly 5.0.7` al servidor. Se replica su descifrado DES-ECB en `SaavnFallbackResolver` (~80 líneas, sin deps). |
| NAudio / LibVLCSharp | ❌ en servidor | Solo para desktop. En web el que suena es el `<audio>` del navegador. |

## Arquitectura (aislamiento)

```
<SetlistPlay.cshtml + rehearsal-player.js> ──fetch──▶ PlaybackController
                                                        │ (único punto de entrada)
                                              AudioResolverFactory (primario→fallback, kill-switch)
                                              ├─ YoutubeExplodeResolver (único que conoce YoutubeExplode)
                                              └─ SaavnFallbackResolver (HttpClient + DES-ECB key 38346591)
                                              IMemoryCache 5h (la URL googlevideo expira: jamás en Mongo)
```

Si YouTube/Saavn cambian su API interna, solo falla `/api/v1/playback/*`; el CRUD de setlists/tracks/ensayos sigue vivo.

## Config (`appsettings.json`)

```json
"Playback": { "Primary": "youtube", "EnableSaavnFallback": true, "CacheMinutes": 300, "MaxSearchResults": 10 }
```

Kill-switch sin desplegar: `EnableSaavnFallback=false`.

## Fallbacks y errores

- URL expirada → el player re-resuelve una vez al ver `error` del `<audio>`; si persiste, muestra el motivo.
- `resolve` por `videoId` de YouTube no hace fallback a Saavn (ids incompatibles); por texto libre sí (search → primer hit → resolve).
- Ambos proveedores caídos → `424 { error, providerErrors[] }`, nunca 500 opaco.
- Reintento: 1 reintento con 600 ms solo en `HttpRequestException`; `VideoUnavailable/Unplayable` no se reintentan.

## Uso

```bash
# Buscar (con sesión o basic)
curl -u user:pass "https://tu-api/api/v1/playback/search?q=luna%20rosa&limit=5"
# Resolver y reproducir: pon streamUrl en <audio src>
curl -u user:pass "https://tu-api/api/v1/playback/resolve?videoId=dQw4w9WgXcQ"
# Setlist listo para el player
curl -u user:pass "https://tu-api/api/v1/playback/setlist/<setlistId>"
```

Para que un item resuelva directo, pega su `youtubeUrl` en el Track del catálogo (`Track.Links.YouTubeUrl`); si no, el player busca por título.

## Riesgos (leer antes de hacerlo público)

APIs internas (YouTube InnerTube / JioSaavn `api.php`): violan ToS, sin SLA, pueden romperse o bloquear la IP del servidor sin aviso. Para ensayo privado el riesgo es aceptable; para fans usa MP3 propios (`IFileService`/Cloudinary) o embeds oficiales.
