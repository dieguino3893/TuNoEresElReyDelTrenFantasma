/* Asistente del panel: hilos, mensajes y propuestas con confirmación. */
(function () {
  "use strict";
  var hiloId = null;
  var enviando = false;
  var lista = document.getElementById("listaHilos");
  var caja = document.getElementById("mensajes");
  var aviso = document.getElementById("aviso");
  var form = document.getElementById("formChat");
  var input = document.getElementById("inputTexto");
  var token = document.querySelector('input[name="__RequestVerificationToken"]');

  function headers() {
    return {
      "Content-Type": "application/json",
      "X-XSRF-TOKEN": token ? token.value : "",
    };
  }

  function mostrarAviso(texto) {
    aviso.textContent = texto;
    aviso.style.display = "block";
  }

  function limpiarAviso() {
    aviso.style.display = "none";
    aviso.textContent = "";
  }

  async function leerError(res) {
    try {
      var d = await res.json();
      return d.mensaje || ("Error " + res.status);
    } catch (e) {
      return "Error " + res.status;
    }
  }

  function burbuja(mensaje) {
    var div = document.createElement("div");
    div.className = "burbuja " + (mensaje.rol === "usuario" ? "usuario" : "asistente");
    div.textContent = mensaje.contenido;
    if (mensaje.propuesta) {
      var btns = document.createElement("div");
      btns.className = "prop-btns";
      var ok = document.createElement("button");
      ok.className = "btn btn-primary btn-sm";
      ok.type = "button";
      ok.textContent = "Confirmar";
      ok.onclick = function () { confirmar(mensaje.propuesta.mensajeId, true, div); };
      var no = document.createElement("button");
      no.className = "btn btn-secondary btn-sm";
      no.type = "button";
      no.textContent = "No";
      no.onclick = function () { confirmar(mensaje.propuesta.mensajeId, false, div); };
      btns.appendChild(ok);
      btns.appendChild(no);
      div.appendChild(btns);
    }
    caja.appendChild(div);
    caja.scrollTop = caja.scrollHeight;
    return div;
  }

  async function cargarHilos() {
    try {
      var res = await fetch("/admin/asistente/hilos", { credentials: "same-origin" });
      if (!res.ok) return;
      var hilos = await res.json();
      lista.innerHTML = "";
      if (hilos.length === 0) {
        var p = document.createElement("p");
        p.className = "muted";
        p.textContent = "Todavía no hay chats.";
        lista.appendChild(p);
      }
      hilos.forEach(function (h) {
        var b = document.createElement("button");
        b.type = "button";
        b.className = "chat-hilo" + (h.id === hiloId ? " active" : "");
        var s = document.createElement("span");
        s.textContent = h.titulo;
        s.title = h.titulo;
        var x = document.createElement("button");
        x.type = "button";
        x.innerHTML = '<i class="fa-solid fa-trash"></i>';
        x.title = "Eliminar chat";
        x.onclick = function (e) { e.stopPropagation(); eliminarHilo(h); };
        b.appendChild(s);
        b.appendChild(x);
        b.onclick = function () { abrirHilo(h.id); };
        lista.appendChild(b);
      });
    } catch (e) { /* sin conexión: el chat avisa al enviar */ }
  }

  async function abrirHilo(id) {
    hiloId = id;
    caja.innerHTML = "";
    limpiarAviso();
    try {
      var res = await fetch("/admin/asistente/hilos/" + id, { credentials: "same-origin" });
      if (!res.ok) { mostrarAviso(await leerError(res)); return; }
      var mensajes = await res.json();
      mensajes.forEach(burbuja);
      cargarHilos();
    } catch (e) {
      mostrarAviso("Sin conexión: el asistente necesita internet.");
    }
  }

  function nuevoChat() {
    hiloId = null;
    caja.innerHTML = "";
    limpiarAviso();
    cargarHilos();
    input.focus();
  }

  async function eliminarHilo(h) {
    if (!window.confirm('¿Eliminar "' + h.titulo + '"? Esta acción no se puede deshacer.')) return;
    try {
      var res = await fetch("/admin/asistente/hilos/" + h.id, {
        method: "DELETE",
        headers: headers(),
        credentials: "same-origin",
      });
      if (!res.ok && res.status !== 204) { mostrarAviso(await leerError(res)); return; }
      if (hiloId === h.id) { hiloId = null; caja.innerHTML = ""; }
      cargarHilos();
    } catch (e) {
      mostrarAviso("Sin conexión: el asistente necesita internet.");
    }
  }

  async function enviar(e) {
    if (e) e.preventDefault();
    var texto = input.value.trim();
    if (!texto || enviando) return;
    if (!navigator.onLine) { mostrarAviso("Sin conexión: el asistente necesita internet."); return; }
    enviando = true;
    input.value = "";
    limpiarAviso();
    burbuja({ rol: "usuario", contenido: texto });
    try {
      var res = await fetch("/admin/asistente/mensaje", {
        method: "POST",
        headers: headers(),
        credentials: "same-origin",
        body: JSON.stringify({ hiloId: hiloId, texto: texto }),
      });
      if (!res.ok) { mostrarAviso(await leerError(res)); return; }
      var respuesta = await res.json();
      burbuja(respuesta);
      if (!hiloId) {
        try {
          var rh = await fetch("/admin/asistente/hilos", { credentials: "same-origin" });
          if (rh.ok) {
            var hs = await rh.json();
            if (hs.length > 0) hiloId = hs[0].id;
          }
        } catch (e2) { /* nada */ }
      }
      cargarHilos();
    } catch (err) {
      mostrarAviso("No se pudo contactar al asistente. Intenta de nuevo.");
    } finally {
      enviando = false;
    }
  }

  async function confirmar(mensajeId, valor, div) {
    var btns = div.querySelectorAll("button");
    btns.forEach(function (b) { b.disabled = true; });
    try {
      var res = await fetch("/admin/asistente/confirmar", {
        method: "POST",
        headers: headers(),
        credentials: "same-origin",
        body: JSON.stringify({ mensajeId: mensajeId, confirmar: valor }),
      });
      if (!res.ok) { mostrarAviso(await leerError(res)); return; }
      div.querySelector(".prop-btns")?.remove();
      burbuja({ rol: "usuario", contenido: valor ? "Sí, confirma la propuesta." : "No, cancela la propuesta." });
      burbuja(await res.json());
    } catch (e) {
      mostrarAviso("No se pudo confirmar. Intenta de nuevo.");
    } finally {
      btns.forEach(function (b) { b.disabled = false; });
    }
  }

  document.getElementById("btnNuevoChat").onclick = nuevoChat;
  form.onsubmit = enviar;
  burbuja({ rol: "asistente", contenido: "Hola, soy el Fantasma. Dime qué buscar o registrar: shows, miembros, noticias, música, setlists o ensayos. Hago la propuesta y te pido confirmación antes de guardar." });
  cargarHilos();
})();
