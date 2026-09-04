# Front — GitHub Pages (vanilla)

Desacoplado del back .NET. Se despliega solo esta carpeta `free-rock-band-website-template/` a GitHub Pages (custom domain + DNS).

## Configurar API

Editar `js/config.js`:
```js
window.__CONFIG__ = { API_URL: "https://tu-api.onrender.com" }
```
Para dev local:
```js
API_URL: "http://localhost:5299"
```

## Estructura

- `index.html` → estático + hidratación dinámica via `js/app.api.js` (fetch `/api/v1/home`)
- `js/config.js` → URL back
- `js/api.js` → cliente fetch
- `js/app.api.js` → reemplaza DOM: hero, redes (1 lista ShowInHero/Footer), nextShow (fecha más cercana), members, shows (click → GoogleMapsUrl), news (ContentHtml), tracks (Spotify/Apple/YouTube manual)

Si el API no responde, queda contenido estático (fallback).

## Deploy GitHub Pages

Opción A (recomendada): mover contenido a `docs/` en rama `main` y activar Pages → Source `main /docs`.

```bash
cp -r free-rock-band-website-template docs
git add docs && git commit -m "deploy front"
git push
```

Opción B: rama `gh-pages`:
```bash
git subtree push --prefix free-rock-band-website-template origin gh-pages
```

Dominio custom: Settings → Pages → Custom domain `tunoereselreydeltrenfantasma.com` + DNS CNAME.

## CORS

El back `.NET` en `appsettings.json:Cors:AllowedOrigins` debe incluir tu dominio Pages:
```
https://TU_USUARIO.github.io
https://tunoereselreydeltrenfantasma.com
```

