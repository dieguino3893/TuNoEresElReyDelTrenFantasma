# Guía Sincronización Música — Spotify / YouTube (manual por ahora)

El modelo `tracks` ya soporta 3 plataformas manuales (Spotify, Apple Music, YouTube). La sincronización automática está preparada pero **desactivada** hasta que configures credenciales.

## Estado actual (manual)

Cada track se crea via `POST /api/v1/tracks` con Basic Auth:

```bash
curl -u <usuario>:<contraseña> -X POST https://tu-api/api/v1/tracks \
 -H "Content-Type: application/json" \
 -d '{
   "title": "Nuevo Single",
   "trackNumber": 6,
   "type": "Single",
   "links": {
     "spotifyUrl": "https://open.spotify.com/intl-es/track/XXX",
     "spotifyTrackId": "XXX",
     "appleMusicUrl": "https://music.apple.com/...",
     "youtubeUrl": "https://youtube.com/watch?v=..."
   },
   "externalId": "XXX",
   "isVisible": true,
   "order": 6
 }'
```

El front vanilla usa `links.spotifyEmbedUrl` (auto-generado si mandas `spotifyTrackId`) para el `iframe` del carrusel.

## Futuro auto — Spotify

1. Ir a https://developer.spotify.com/dashboard → Create App
2. Copiar `Client ID` y `Client Secret`
3. Poner en `.env` (nunca en `appsettings.json`):
```
Spotify__ClientId=xxx
Spotify__ClientSecret=yyy
Spotify__ArtistId=<TU_SPOTIFY_ARTIST_ID>
```
4. `MusicSyncService.SyncFromSpotifyAsync()` hará:
   `POST https://accounts.spotify.com/api/token` (client_credentials) → `GET https://api.spotify.com/v1/artists/{id}/albums?include_groups=single` → upsert en `tracks` donde `source=SpotifyApi`.

**Regla:** si un track ya tiene `source=Manual`, no se sobrescribe.

## Futuro auto — YouTube

1. https://console.cloud.google.com → APIs → habilitar YouTube Data API v3 → Crear API Key
2. `.env`:
```
YouTube__ApiKey=xxx
YouTube__ChannelId=<TU_YOUTUBE_CHANNEL_ID>
```
3. `GET https://www.googleapis.com/youtube/v3/search?channelId=...&part=snippet&type=video`

## Activación

Cuando tengas credenciales, descomenta `MusicSyncService` y agrega un `BackgroundService` o endpoint `POST /api/v1/admin/sync/tracks`.

