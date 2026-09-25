/* Player de ensayo interno — El Rey del Tren Fantasma.
 * La API (/api/v1/playback) resuelve metadatos + URL firmada; este <audio> reproduce.
 * Controles: play, pause, anterior/siguiente, seek, volumen. Sin anuncios (stream directo).
 * Uso: SetlistPlay.cshtml lo inicializa con window.__SETLIST_ID__.
 */
(function () {
  "use strict";

  async function api(path) {
    const r = await fetch(path, { credentials: "same-origin" });
    if (!r.ok) {
      let msg = "Error " + r.status;
      try { const j = await r.json(); msg = j.error || msg; } catch { /* texto */ }
      throw new Error(msg);
    }
    return r.json();
  }

  function fmt(sec) {
    if (sec == null || isNaN(sec)) return "—";
    sec = Math.max(0, Math.floor(sec));
    return Math.floor(sec / 60) + ":" + String(sec % 60).padStart(2, "0");
  }

  window.RehearsalPlayer = {
    items: [], idx: -1, audio: null, statusEl: null, titleEl: null,

    async init(setlistId) {
      this.audio = document.getElementById("rp-audio");
      this.statusEl = document.getElementById("rp-status");
      this.titleEl = document.getElementById("rp-now");
      const list = document.getElementById("rp-list");
      const vol = document.getElementById("rp-vol");
      const seek = document.getElementById("rp-seek");

      if (vol) vol.addEventListener("input", () => { this.audio.volume = vol.value; });
      if (seek) seek.addEventListener("input", () => {
        if (this.audio.duration) this.audio.currentTime = seek.value * this.audio.duration;
      });
      this.audio.addEventListener("timeupdate", () => {
        if (seek && this.audio.duration) seek.value = this.audio.currentTime / this.audio.duration;
        const t = document.getElementById("rp-time");
        if (t) t.textContent = fmt(this.audio.currentTime) + " / " + fmt(this.audio.duration);
      });
      this.audio.addEventListener("ended", () => this.next());
      // Si la URL firmada expiró (403 a mitad de pista), re-resolver una vez.
      this.audio.addEventListener("error", async () => {
        const cur = this.items[this.idx];
        if (cur && !cur._retried) {
          cur._retried = true;
          this.setStatus("URL expirada, reintentando…");
          try { await this.playAt(this.idx); } catch (e) { this.setStatus("Sin audio: " + e.message); }
        }
      });

      document.getElementById("rp-play").onclick = () => this.audio.paused ? this.audio.play() : this.audio.pause();
      document.getElementById("rp-prev").onclick = () => this.prev();
      document.getElementById("rp-next").onclick = () => this.next();

      const searchBtn = document.getElementById("rp-search-btn");
      if (searchBtn) searchBtn.onclick = () => this.search();

      this.setStatus("Cargando setlist…");
      const data = await api("/api/v1/playback/setlist/" + encodeURIComponent(setlistId));
      this.items = data.items || [];
      list.innerHTML = "";
      this.items.forEach((it, i) => {
        const li = document.createElement("li");
        li.className = "rp-item";
        li.innerHTML = "<button class='btn btn-secondary btn-sm'>▶</button> " +
          "<strong></strong> <small></small>";
        li.querySelector("strong").textContent = (it.position || (i + 1)) + ". " + it.title;
        li.querySelector("small").textContent = it.videoId ? "YouTube ✓" : "buscar al reproducir";
        li.querySelector("button").onclick = () => this.playAt(i);
        list.appendChild(li);
      });
      this.setStatus(this.items.length ? "Listo: " + this.items.length + " temas." : "Setlist vacío.");
    },

    async playAt(i) {
      const it = this.items[i];
      if (!it) return;
      this.idx = i;
      this.setStatus("Resolviendo: " + it.title + "…");
      this.titleEl.textContent = "♪ " + it.title;
      // resolve acepta videoId o texto libre (título del setlist → primer hit de YouTube).
      const key = it.videoId
        ? "videoId=" + encodeURIComponent(it.videoId)
        : "q=" + encodeURIComponent(it.title);
      const track = await api("/api/v1/playback/resolve?" + key);
      this.audio.src = track.streamUrl;
      this.titleEl.textContent = "♪ " + track.title + (track.artist ? " — " + track.artist : "") +
        " [" + track.provider + "]";
      await this.audio.play();
      this.setStatus("Sonando (" + track.provider + ", expira " +
        new Date(track.expiresAt).toLocaleTimeString() + ")");
      document.querySelectorAll(".rp-item").forEach((el, j) =>
        el.classList.toggle("active", j === i));
    },

    async search() {
      const q = document.getElementById("rp-q").value.trim();
      if (q.length < 2) return;
      this.setStatus("Buscando…");
      const hits = await api("/api/v1/playback/search?q=" + encodeURIComponent(q));
      const box = document.getElementById("rp-hits");
      box.innerHTML = "";
      hits.forEach(h => {
        const li = document.createElement("li");
        li.innerHTML = "<button class='btn btn-secondary btn-sm'>▶</button> <strong></strong> <small></small>";
        li.querySelector("strong").textContent = h.title;
        li.querySelector("small").textContent = (h.artist || "") + " · " + fmt(h.durationSeconds) + " [" + h.provider + "]";
        li.querySelector("button").onclick = async () => {
          this.items.splice(this.idx + 1, 0, { title: h.title, videoId: h.videoId });
          await this.playAt(this.idx + 1);
        };
        box.appendChild(li);
      });
      this.setStatus(hits.length ? hits.length + " resultados." : "Sin resultados.");
    },

    prev() { if (this.idx > 0) this.playAt(this.idx - 1).catch(e => this.setStatus("Sin audio: " + e.message)); },
    next() {
      if (this.idx + 1 < this.items.length)
        this.playAt(this.idx + 1).catch(e => this.setStatus("Sin audio: " + e.message));
    },
    setStatus(t) { if (this.statusEl) this.statusEl.textContent = t; }
  };
})();
