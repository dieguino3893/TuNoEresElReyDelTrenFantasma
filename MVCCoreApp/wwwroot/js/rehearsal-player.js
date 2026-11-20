/* Player de ensayo interno — El Rey del Tren Fantasma.
 * La API (/api/v1/playback) resuelve metadatos + URL firmada; este <audio> reproduce.
 * Controles: play, pause, anterior/siguiente, seek, volumen. Sin anuncios (stream directo).
 * Uso: SetlistPlay.cshtml lo inicializa con window.__SETLIST_ID__.
 */
(function () {
  "use strict";

  async function api(path) {
    // Propaga la llave del link de ensayo (?key=...) a la API.
    const key = new URLSearchParams(location.search).get("key");
    if (key && !/[?&]key=/.test(path)) path += (path.includes("?") ? "&" : "?") + "key=" + encodeURIComponent(key);
    const r = await fetch(path, { credentials: "same-origin" });
    if (!r.ok) {
      let msg = "Error " + r.status, payload = null;
      try { payload = await r.json(); msg = payload.error || msg; } catch { /* texto */ }
      const err = new Error(msg);
      err.payload = payload;
      throw err;
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
    embedWrap: null, player: null, ytLoading: false, serverBroken: false,

    async init(setlistId) {
      this.audio = document.getElementById("rp-audio");
      this.statusEl = document.getElementById("rp-status");
      this.titleEl = document.getElementById("rp-now");
      this.embedWrap = document.getElementById("rp-embed-wrap");
      const list = document.getElementById("rp-list");
      const vol = document.getElementById("rp-vol");
      const seek = document.getElementById("rp-seek");

      if (vol) vol.addEventListener("input", () => {
        if (this.usingEmbed()) { try { this.player.unMute(); this.player.setVolume(Math.round(vol.value * 100)); } catch { /* aún no listo */ } return; }
        this.audio.volume = vol.value;
      });
      if (seek) seek.addEventListener("input", () => {
        if (this.usingEmbed()) { try { const d = this.player.getDuration(); if (d) this.player.seekTo(seek.value * d, true); } catch { /* aún no listo */ } return; }
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

      document.getElementById("rp-play").onclick = () => {
        if (this.usingEmbed()) {
          try {
            const state = this.player.getPlayerState();
            state === 1 ? this.player.pauseVideo() : this.player.playVideo();
          } catch { /* aún no listo */ }
          return;
        }
        this.audio.paused ? this.audio.play() : this.audio.pause();
      };
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
      this.titleEl.textContent = "♪ " + it.title;
      if (this.serverBroken && it.videoId && !it.videoId.startsWith("saavn:")) return this.playEmbed(it.videoId);

      this.setStatus("Resolviendo: " + it.title + "…");
      // resolve acepta videoId o texto libre (título del setlist → primer hit de YouTube).
      const key = it.videoId
        ? "videoId=" + encodeURIComponent(it.videoId)
        : "q=" + encodeURIComponent(it.title);
      try {
        const track = await api("/api/v1/playback/resolve?" + key);
        this.stopTick();
        this.player?.stopVideo();
        if (this.embedWrap) this.embedWrap.hidden = true;
        this.audio.hidden = false;
        this.audio.src = track.streamUrl;
        this.titleEl.textContent = "♪ " + track.title + (track.artist ? " — " + track.artist : "") +
          " [" + track.provider + "]";
        await this.audio.play();
        this.setStatus("Sonando (" + track.provider + ", expira " +
          new Date(track.expiresAt).toLocaleTimeString() + ")");
      } catch (e) {
        // El servidor de producción está en un hosting que YouTube bloquea: el navegador sí puede.
        // Segundo intento por título: deja que el backend use Deezer u otro fallback.
        if (it.videoId) {
          try {
            const alt = await api("/api/v1/playback/resolve?q=" + encodeURIComponent(it.title));
            this.stopTick();
            this.player?.stopVideo();
            if (this.embedWrap) this.embedWrap.hidden = true;
            this.audio.hidden = false;
            this.audio.src = alt.streamUrl;
            this.titleEl.textContent = "♪ " + alt.title + (alt.artist ? " — " + alt.artist : "") +
              " [" + alt.provider + "]";
            await this.audio.play();
            this.setStatus("Sonando (" + alt.provider + ").");
            document.querySelectorAll(".rp-item").forEach((el, j) =>
              el.classList.toggle("active", j === i));
            return;
          } catch { /* sin fallback alterno */ }
        }
        if (!it.videoId || it.videoId.startsWith("saavn:")) {
          // El servidor encontró el video pero no pudo resolver audio: usa ese videoId en el embed.
          it.videoId = e.payload?.videoId || null;
        }
        if (!it.videoId) {
          try {
            const hits = await api("/api/v1/playback/search?q=" + encodeURIComponent(it.title));
            if (hits[0]?.provider === "youtube") it.videoId = hits[0].videoId;
          } catch { /* sin búsqueda disponible */ }
        }
        if (!it.videoId) { this.setStatus("Sin audio: " + e.message); return; }
        this.serverBroken = true;
        this.setStatus("YouTube bloquea al servidor; usando reproductor embed.");
        this.playEmbed(it.videoId);
      }
      document.querySelectorAll(".rp-item").forEach((el, j) =>
        el.classList.toggle("active", j === i));
    },

    usingEmbed() { return !!(this.player && this.embedWrap && !this.embedWrap.hidden); },

    setPlayIcon(playing) {
      const btn = document.getElementById("rp-play");
      if (btn) btn.innerHTML = playing
        ? '<i class="fa-solid fa-pause"></i>'
        : '<i class="fa-solid fa-play"></i>';
    },

    startTick() {
      this.stopTick();
      // El embed no emite timeupdate: se sondea posición y estado.
      this.tickTimer = setInterval(() => {
        if (!this.usingEmbed()) return;
        try {
          const d = this.player.getDuration() || 0;
          const c = this.player.getCurrentTime() || 0;
          const seek = document.getElementById("rp-seek");
          const t = document.getElementById("rp-time");
          if (seek && d) seek.value = c / d;
          if (t) t.textContent = fmt(c) + " / " + fmt(d);
          this.setPlayIcon(this.player.getPlayerState() === 1);
        } catch { /* aún no listo */ }
      }, 500);
    },

    stopTick() { if (this.tickTimer) { clearInterval(this.tickTimer); this.tickTimer = null; } },

    playEmbed(videoId) {
      this.stopTick();
      this.audio.pause();
      this.audio.removeAttribute("src");
      this.audio.hidden = true;
      this.embedWrap.hidden = false;
      this.setPlayIcon(true);
      const boot = () => {
        if (this.player) { try { this.player.loadVideoById(videoId); } catch { /* aún no listo */ } return; }
        this.player = new YT.Player("rp-embed", {
          videoId,
          playerVars: { autoplay: 1, controls: 0, disablekb: 1, fs: 0, rel: 0, modestbranding: 1, iv_load_policy: 3, playsinline: 1 },
          events: {
            onReady: (e) => {
              try {
                const vol = document.getElementById("rp-vol");
                if (vol) { e.target.unMute(); e.target.setVolume(Math.round(vol.value * 100)); }
                e.target.playVideo();
              } catch { /* aún no listo */ }
            },
            onStateChange: (e) => {
              if (e.data === 0) this.next();
              else if (e.data === 1) this.setPlayIcon(true);
              else if (e.data === 2) this.setPlayIcon(false);
            }
          }
        });
      };
      if (window.YT?.Player) boot();
      else {
        window.onYouTubeIframeAPIReady = boot;
        if (!this.ytLoading) {
          this.ytLoading = true;
          const s = document.createElement("script");
          s.src = "https://www.youtube.com/iframe_api";
          document.head.appendChild(s);
        }
      }
      this.startTick();
      this.setStatus("Reproduciendo en YouTube (embed, puede mostrar anuncios).");
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
