# Asistente del panel ("el Fantasma")

Port del chatbot del taller pespunte (`TemerarIA`), adaptado al sitio y solo para admins.

## Qué hace

- Chat en `/admin/asistente` con hilos por admin (colecciones `chatHilos`, `chatMensajes`).
- 7 herramientas genéricas: `consultar`, `reporte`, `calcular`, `crear`, `actualizar`,
  `eliminar` y `confirmar_pendiente`, sobre shows, members, news, tracks, setlists y rehearsals.
- Escrituras con confirmación previa y expiración a 15 min. Bajas lógicas (`isDeleted`).
- Bloqueado en el backend: usuarios, API keys, suscriptores, archivos, ajustes y todo lo
  que no sea una de las 6 entidades.

## Config

Sin `Asistente:ApiKey` el chat muestra aviso y no responde (mismo esquema que pespunte):

```
Asistente__ApiKey=<key de OpenCode Go>
```

Opcionales en `appsettings.json`: `Asistente:BaseUrl`, `Asistente:Modelo`, `Asistente:MaxTokens`.

Modelo actual: `deepseek-v4.1-flash` (variante low, salida hasta 16000 tokens).

## Límites

30/min por usuario (RateLimiter `asistente`) + 3/seg y 300/hora (`LimiteAsistente`).

## Archivos

`Models/HiloChat.cs`, `Models/MensajeChat.cs`, `Dtos/ChatDtos.cs`,
`Services/Asistente/*` (7), `Controllers/AsistenteController.cs`,
`Views/Admin/Asistente.cshtml`, `wwwroot/js/asistente.js`.
