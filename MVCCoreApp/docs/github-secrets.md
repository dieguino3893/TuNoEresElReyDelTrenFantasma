# Secrets de GitHub — deploy del back

Repo: `TuNoEresElReyDelTrenFantasma`. Ruta: **Settings → Secrets and variables → Actions → New repository secret**.
Los usa `.github/workflows/desplegar.yml`. Nombres exactos y sensibles a mayúsculas.

| Secret | Valor a pegar (de tu `.env` local) | Obligatorio |
|---|---|---|
| `MONGO_CONN` | `MongoDbSettings__ConnectionString` | Sí |
| `MONGO_DB` | `MongoDbSettings__DatabaseName` | Sí |
| `ASISTENTE_KEY` | `Asistente__ApiKey` | Solo si usas `/admin/asistente` |
| `FRONTEND_URL` | `Frontend__PublicUrl` | Sí |
| `FTP_SERVIDOR` | Host FTPS del hosting (p. ej. `win1234.smarterasp.net`) | Sí |
| `FTP_USUARIO` | Usuario FTP del hosting | Sí |
| `FTP_CONTRASENA` | Contraseña FTP del hosting | Sí |

## Ramas

- Push a `back_dev`: solo compila y audita (verifica en verde antes de desplegar).
- Push a `back_main`: compila, genera `appsettings.Production.json` con estos secrets y sube por FTPS.

## Notas

- Los nombres del `.env` (con `__`) son solo locales; en GitHub van los cortos de la tabla.
- El workflow no publica claves de Cloudinary/Spotify/YouTube: si el admin necesita subir archivos en producción, hay que agregarlas al workflow.
