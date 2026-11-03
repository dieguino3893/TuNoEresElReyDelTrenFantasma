# Despliegue del back (rama `back`)

Workflow `.github/workflows/desplegar.yml` (réplica del taller pespunte, sin SPA):
push a `back` → restore bloqueado → auditoría de paquetes → build → publish →
`appsettings.Production.json` desde secrets → FTPS con `app_offline.htm`.

## Secrets requeridos (repo → Settings → Secrets → Actions)

| Secret | Uso |
|---|---|
| `MONGO_CONN` | `MongoDbSettings:ConnectionString` en producción |
| `MONGO_DB` | `MongoDbSettings:DatabaseName` |
| `ASISTENTE_KEY` | `Asistente:ApiKey` (vacía = asistente desactivado) |
| `FRONTEND_URL` | `Frontend:PublicUrl` + origen CORS |
| `FTP_SERVIDOR` | Host FTPS |
| `FTP_USUARIO` | Usuario FTPS |
| `FTP_CONTRASENA` | Contraseña FTPS |

## Respaldo

`.github/workflows/respaldar.yml`: `mongodump` diario a Artifacts (30 días).
Requiere permitir las IPs de GitHub Actions en Atlas (Network Access).
En M0 usa el respaldo automático de Atlas y desactiva el workflow.

## Medidas replicadas de pespunte

- Login con rate limit (10/min por IP), bloqueo 5 fallos → 15 min, auditoría
  (`login`, `login_fallido`, `login_bloqueado`) en colección `auditLog`.
- Antiforgery en todos los POST del panel + login/logout (logout solo POST).
- Asistente: escrituras confirmadas se auditan (`asistente_ejecuta`).
- Usuarios: altas/bajas auditadas (`usuario_creado`, `usuario_eliminado`).
- Headers: CSP/DENY/nosniff, `Cache-Control: no-store` en `/api`, sin header `Server`,
  HSTS 365 días, HTTPS solo fuera de dev, `ForwardedHeaders` tras proxy.
- Dependencias bloqueadas (`packages.lock.json` + `--locked-mode` en CI).
