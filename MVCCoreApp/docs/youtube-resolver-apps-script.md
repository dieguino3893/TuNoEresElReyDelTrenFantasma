# Resolver de YouTube con Google Apps Script

El servidor de producción (Hetzner) recibe `Sign in to confirm you're not a bot` de YouTube.
Apps Script ejecuta el fetch desde IPs de Google, así que YouTube no lo bloquea.

## 1. Crear el script

1. Entra a https://script.google.com con la cuenta de Google de la banda.
2. Nuevo proyecto.
3. Pega este código en `Código.gs`:

```javascript
function doGet(e) {
  var id = e && e.parameter && e.parameter.id;
  if (!id) return json({ error: "falta id" });

  var body = {
    videoId: id,
    contentCheckOk: true,
    racyCheckOk: true,
    context: {
      client: {
        clientName: "ANDROID",
        clientVersion: "20.10.38",
        androidSdkVersion: 30,
        hl: "en",
        gl: "US"
      }
    }
  };

  var res = UrlFetchApp.fetch("https://www.youtube.com/youtubei/v1/player?prettyPrint=false", {
    method: "post",
    contentType: "application/json",
    payload: JSON.stringify(body),
    headers: {
      "User-Agent": "com.google.android.youtube/20.10.38 (Linux; U; Android 11) gzip"
    },
    muteHttpExceptions: true
  });

  var j;
  try { j = JSON.parse(res.getContentText()); }
  catch (err) { return json({ error: "respuesta no JSON" }); }

  var ps = j.playabilityStatus || {};
  if (ps.status !== "OK") return json({ error: ps.status + " " + (ps.reason || "") });

  var formats = (j.streamingData && j.streamingData.adaptiveFormats) || [];
  formats = formats.filter(function (f) {
    return (f.mimeType || "").indexOf("audio") === 0 && f.url;
  });
  formats.sort(function (a, b) { return (b.bitrate || 0) - (a.bitrate || 0); });

  var f = formats[0];
  if (!f) return json({ error: "sin stream de audio" });

  var det = j.videoDetails || {};
  return json({
    url: f.url,
    title: det.title || "",
    author: det.author || "",
    duration: Number(det.lengthSeconds || 0)
  });
}

function json(obj) {
  return ContentService.createTextOutput(JSON.stringify(obj))
    .setMimeType(ContentService.MimeType.JSON);
}
```

## 2. Publicar

1. Implementar → Nueva implementación.
2. Tipo: Aplicación web.
3. Ejecutar como: yo. Quién tiene acceso: cualquiera.
4. Autoriza los permisos y copia la URL `/exec`.

## 3. Configurar

GitHub → Settings → Secrets and variables → Actions → New secret:

- `RESOLVER_URL` = la URL `/exec` copiada.

Luego Actions → "Compilar y desplegar" → Run workflow.

Prueba directa:

```
https://script.google.com/macros/s/XXXX/exec?id=rwUgHFN32yI
```

Debe devolver `{"url":"https://...googlevideo...","title":"..."}`.
