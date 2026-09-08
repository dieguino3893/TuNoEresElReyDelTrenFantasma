# Back — .NET 10 API + Admin

Desplegar en servicio aparte (Render, Railway, Fly.io, Azure). Front en GitHub Pages lo consume via CORS.

## Env

`.env` en `MVCCoreApp/` (ver `.env.example`):
```
MongoDbSettings__ConnectionString=mongodb+srv://...
MongoDbSettings__DatabaseName=ElReyDelTrenFantasma
Cors__AllowedOrigins__0=https://TU_USUARIO.github.io
Cors__AllowedOrigins__1=https://TU_DOMINIO_PRODUCCION.tld
```

## Endpoints públicos (sin auth, CORS)

- `GET /health`
- `GET /api/v1/home` → { settings, members, nextShow, shows, news, tracks }
- `GET /api/v1/site-settings`
- `GET /api/v1/members`
- `GET /api/v1/shows` y `/next`
- `GET /api/v1/news` y `/news/slug/{slug}`
- `GET /api/v1/tracks`

## Endpoints protegidos (Basic Auth vs `users` Mongo)

Los usuarios viven solo en la colección `users` de MongoDB. No hay seed ni fallback
de admin: si la colección está vacía, nadie puede entrar hasta dar de alta el primer
usuario directamente en la base de datos (ver "Primer usuario" abajo).
Header: `Authorization: Basic base64(user:pass)`

- `PUT /api/v1/site-settings`
- `POST/PUT/DELETE /api/v1/members` (+ `GET /members/admin`)
- `POST/PUT/DELETE /api/v1/shows`
- `POST/PUT/DELETE /api/v1/news`
- `POST/PUT/DELETE /api/v1/tracks`
- `POST /admin/seed` (solo Admin, verifica índices)
- `GET /api/v1/admin/stats`

Trazabilidad: cada doc guarda `createdAt/By, updatedAt/By, isDeleted` y `updatedBy` se setea con `User.Identity.Name`.

## Admin Panel (Razor)

- `GET /admin` → dashboard (stats, links)
- `GET /admin/members|shows|news|tracks|settings|users` (requiere Basic Auth, navegador pedirá usuario)
- `POST /admin/seed` (verifica índices)

Tecnología: Razor MVC + Bootstrap 5 (ya en wwwroot).

## Índices

Al arrancar (`Program.cs`) y con `POST /admin/seed` solo se verifican los índices de
MongoDB. No se crea ningún usuario ni contenido.

## Primer usuario (alta manual en MongoDB)

Sin usuarios en la colección `users` nadie puede entrar al panel ni a la API.
El primer admin se da de alta directamente en la base de datos con un hash BCrypt
de su contraseña (coste 11, como genera `BCrypt.Net`):

```js
db.users.insertOne({
  username: "<tu_usuario>",
  passwordHash: "<hash_bcrypt_de_tu_contraseña>",
  role: "Admin",
  isActive: true,
  isDeleted: false,
  createdAt: new Date(),
  createdBy: "bootstrap",
  updatedAt: new Date(),
  updatedBy: "bootstrap"
})
```

Después entra en `/auth/login` y gestiona el resto desde `/admin/users`.

## Música manual

Solo 3 plataformas por track: `spotifyUrl/appleMusicUrl/youtubeUrl`. Guía futura Spotify/YouTube en `docs/music-sync-guide.md`.

