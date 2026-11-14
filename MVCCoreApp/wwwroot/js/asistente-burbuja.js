/* Fantasma en burbuja arrastrable: mismo API del asistente (/admin/asistente/...). */
(function () {
  "use strict";
  var burbuja = document.getElementById("fantasmaBurbuja");
  var cabeza = document.getElementById("fantasmaCabeza");
  var cuerpo = document.getElementById("fantasmaCuerpo");
  var fab = document.getElementById("fantasmaFab");
  var caja = document.getElementById("fantasmaMensajes");
  var aviso = document.getElementById("fantasmaAviso");
  var form = document.getElementById("fantasmaForm");
  var input = document.getElementById("fantasmaInput");
  var selHilos = document.getElementById("fantasmaHilos");
  if (!burbuja || !fab) return;
  var hiloId = null, enviando = false, saludado = false;
  var token = burbuja.querySelector('input[name="__RequestVerificationToken"]');
  try {
    var pos = JSON.parse(localStorage.getItem("fantasmaPos") || "null");
    if (pos) { burbuja.style.left = pos.x + "px"; burbuja.style.top = pos.y + "px"; burbuja.style.right = "auto"; burbuja.style.bottom = "auto"; }
  } catch (e) {}

  function headers() { return { "Content-Type": "application/json", "X-XSRF-TOKEN": token ? token.value : "" }; }
  function mostrarAviso(t) { aviso.textContent = t; aviso.style.display = "block"; }
  function limpiarAviso() { aviso.style.display = "none"; aviso.textContent = ""; }
  async function leerError(res) { try { var d = await res.json(); return d.mensaje || ("Error " + res.status); } catch (e) { return "Error " + res.status; } }
  function ver() { burbuja.hidden = false; fab.style.display = "none"; if (!saludado) { saludado = true; burbujaMsg({ rol: "asistente", contenido: "Hola, soy el Fantasma. Dime qué buscar o registrar." }); } input.focus(); }
  function ocultar() { burbuja.hidden = true; fab.style.display = "grid"; }

  function burbujaMsg(m) {
    var div = document.createElement("div");
    div.className = "burbuja " + (m.rol === "usuario" ? "usuario" : "asistente");
    div.textContent = m.contenido;
    if (m.propuesta) {
      var btns = document.createElement("div");
      btns.className = "prop-btns";
      var ok = document.createElement("button");
      ok.className = "btn btn-primary btn-sm"; ok.type = "button"; ok.textContent = "Confirmar";
      ok.onclick = function () { confirmar(m.propuesta.mensajeId, true, div); };
      var no = document.createElement("button");
      no.className = "btn btn-secondary btn-sm"; no.type = "button"; no.textContent = "No";
      no.onclick = function () { confirmar(m.propuesta.mensajeId, false, div); };
      btns.appendChild(ok); btns.appendChild(no); div.appendChild(btns);
    }
    caja.appendChild(div); caja.scrollTop = caja.scrollHeight;
    return div;
  }

  async function cargarHilos() {
    try {
      var res = await fetch("/admin/asistente/hilos", { credentials: "same-origin" });
      if (!res.ok) return;
      var hilos = await res.json();
      selHilos.innerHTML = "";
      var opt0 = document.createElement("option");
      opt0.value = ""; opt0.textContent = hilos.length ? "Elegir chat…" : "Nuevo chat";
      selHilos.appendChild(opt0);
      hilos.forEach(function (h) {
        var o = document.createElement("option");
        o.value = h.id; o.textContent = h.titulo;
        if (h.id === hiloId) o.selected = true;
        selHilos.appendChild(o);
      });
    } catch (e) {}
  }

  async function abrirHilo(id) {
    hiloId = id || null; caja.innerHTML = ""; limpiarAviso();
    if (!id) return;
    try {
      var res = await fetch("/admin/asistente/hilos/" + id, { credentials: "same-origin" });
      if (!res.ok) { mostrarAviso(await leerError(res)); return; }
      (await res.json()).forEach(burbujaMsg);
    } catch (e) { mostrarAviso("Sin conexión: el asistente necesita internet."); }
  }

  async function enviar(e) {
    if (e) e.preventDefault();
    var texto = input.value.trim();
    if (!texto || enviando) return;
    if (!navigator.onLine) { mostrarAviso("Sin conexión: el asistente necesita internet."); return; }
    enviando = true; input.value = ""; limpiarAviso();
    burbujaMsg({ rol: "usuario", contenido: texto });
    try {
      var res = await fetch("/admin/asistente/mensaje", {
        method: "POST", headers: headers(), credentials: "same-origin",
        body: JSON.stringify({ hiloId: hiloId, texto: texto }),
      });
      if (!res.ok) { mostrarAviso(await leerError(res)); return; }
      burbujaMsg(await res.json());
      cargarHilos();
    } catch (err) { mostrarAviso("No se pudo contactar al asistente. Intenta de nuevo."); }
    finally { enviando = false; }
  }

  async function confirmar(mensajeId, valor, div) {
    var btns = div.querySelectorAll("button");
    btns.forEach(function (b) { b.disabled = true; });
    try {
      var res = await fetch("/admin/asistente/confirmar", {
        method: "POST", headers: headers(), credentials: "same-origin",
        body: JSON.stringify({ mensajeId: mensajeId, confirmar: valor }),
      });
      if (!res.ok) { mostrarAviso(await leerError(res)); return; }
      div.querySelector(".prop-btns")?.remove();
      burbujaMsg({ rol: "usuario", contenido: valor ? "Sí, confirma la propuesta." : "No, cancela la propuesta." });
      burbujaMsg(await res.json());
    } catch (e) { mostrarAviso("No se pudo confirmar. Intenta de nuevo."); }
    finally { btns.forEach(function (b) { b.disabled = false; }); }
  }

  // Arrastre por cabecera (mouse + táctil).
  (function () {
    var x0 = 0, y0 = 0, l0 = 0, t0 = 0, moviendo = false;
    cabeza.addEventListener("pointerdown", function (e) {
      if (e.target.closest("button,a")) return;
      moviendo = true;
      var r = burbuja.getBoundingClientRect();
      x0 = e.clientX; y0 = e.clientY; l0 = r.left; t0 = r.top;
      cabeza.setPointerCapture(e.pointerId);
      e.preventDefault();
    });
    cabeza.addEventListener("pointermove", function (e) {
      if (!moviendo) return;
      var nx = Math.min(Math.max(0, l0 + e.clientX - x0), window.innerWidth - 80);
      var ny = Math.min(Math.max(0, t0 + e.clientY - y0), window.innerHeight - 60);
      burbuja.style.left = nx + "px"; burbuja.style.top = ny + "px";
      burbuja.style.right = "auto"; burbuja.style.bottom = "auto";
    });
    cabeza.addEventListener("pointerup", function () {
      if (!moviendo) return;
      moviendo = false;
      try { localStorage.setItem("fantasmaPos", JSON.stringify({ x: parseInt(burbuja.style.left) || 0, y: parseInt(burbuja.style.top) || 0 })); } catch (e) {}
    });
  })();

  fab.addEventListener("click", function () { ver(); cargarHilos(); });
  document.getElementById("fantasmaCerrar").addEventListener("click", ocultar);
  document.getElementById("fantasmaMinim").addEventListener("click", function () {
    var oculta = cuerpo.style.display === "none";
    cuerpo.style.display = oculta ? "" : "none";
  });
  document.getElementById("fantasmaNuevo").addEventListener("click", function () { abrirHilo(null); cargarHilos(); });
  selHilos.addEventListener("change", function () { abrirHilo(selHilos.value || null); });
  form.addEventListener("submit", enviar);
})();
