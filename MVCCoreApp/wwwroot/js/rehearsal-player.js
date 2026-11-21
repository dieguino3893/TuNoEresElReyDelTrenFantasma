/* Player de ensayo interno — El Rey del Tren Fantasma.
 * Reproduce con el iframe de YouTube y controles propios (tiempo, volumen, seek).
 * Modo "solo audio": oculta el video y deja sonando el iframe.
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
    items: [], idx: -1, statusEl: null, titleEl: null,
    embedWrap: null, player: null, ytLoading: false, tickTimer: null, audioOnly: false,

    async init(setlistId) {
      this.statusEl = document.getElementById("rp-status");
      this.titleEl = document.getElementById("rp-now");
      this.embedWrap = document.getElementById("rp-embed-wrap");
      const list = document.getElementById("rp-list");
      const vol = document.getElementById("rp-vol");
      const seek = document.getElementById("rp-seek");

      if (vol) vol.addEventListener("input", () => {
        if (!this.player) return;
        try { this.player.unMute(); this.player.setVolume(Math.round(Number(vol.value) * 100)); } catch { /* aún no listo */ }
      });
      if (seek) seek.addEventListener("input", () => {
        if (!this.player) return;
        try { const d = this.player.getDuration(); if (d) this.player.seekTo(Number(seek.value) * d, true); } catch { /* aún no listo */ }
      });

      document.getElementById("rp-play").onclick = () => {
        if (!this.player) return;
        try {
          const state = this.player.getPlayerState();
          state === 1 ? this.player.pauseVideo() : this.player.playVideo();
        } catch { /* aún no listo */ }
      };
      document.getElementById("rp-prev").onclick = () => this.prev();
      document.getElementById("rp-next").onclick = () => this.next();

      const toggle = document.getElementById("rp-video-toggle");
      if (toggle) toggle.onclick = () => {
        this.audioOnly = !this.audioOnly;
        this.embedWrap.classList.toggle("audio-only", this.audioOnly);
        toggle.textContent = this.audioOnly ? "Mostrar video" : "Solo audio";
      };

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
        li.querySelector("small").textContent = it.videoId ? "YouTube" : "buscar al reproducir";
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
      this.setStatus("Buscando: " + it.title + "…");

      let videoId = it.videoId && !it.videoId.startsWith("saavn:") ? it.videoId : null;
      if (!videoId) {
        try {
          const hits = await api("/api/v1/playback/search?q=" + encodeURIComponent(it.title));
          const yt = hits.find(h => h.provider === "youtube");
          videoId = yt ? yt.videoId : null;
        } catch { /* sin búsqueda */ }
      }
      if (!videoId) { this.setStatus("Sin video para " + it.title); return; }

      it.videoId = videoId;
      this.playEmbed(videoId);
      document.querySelectorAll(".rp-item").forEach((el, j) =>
        el.classList.toggle("active", j === i));
    },

    playEmbed(videoId) {
      const boot = () => {
        if (this.player) {
          try { this.player.loadVideoById(videoId); this.player.playVideo(); } catch { /* aún no listo */ }
          return;
        }
        this.player = new YT.Player("rp-embed", {
          videoId,
          playerVars: {
            autoplay: 1, controls: 0, disablekb: 1, fs: 0, rel: 0,
            modestbranding: 1, iv_load_policy: 3, playsinline: 1
          },
          events: {
            onReady: (e) => {
              try {
                const vol = document.getElementById("rp-vol");
                e.target.unMute();
                e.target.setVolume(vol ? Math.round(Number(vol.value) * 100) : 100);
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

      if (window.YT && window.YT.Player) boot();
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
      this.setStatus("Sonando en YouTube.");
      this.setPlayIcon(true);
    },

    setPlayIcon(playing) {
      const btn = document.getElementById("rp-play");
      if (btn) btn.innerHTML = playing
        ? '<i class="fa-solid fa-pause"></i>'
        : '<i class="fa-solid fa-play"></i>';
    },

    startTick() {
      this.stopTick();
      this.tickTimer = setInterval(() => {
        if (!this.player) return;
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

    async search() {
      const q = document.getElementById("rp-q").value.trim();
      if (q.length < 2) return;
      this.setStatus("Buscando…");
      const hits = await api("/api/v1/playback/search?q=" + encodeURIComponent(q));
      const box = document.getElementById("rp-hits");
      box.innerHTML = "";
      const ytHits = hits.filter(h => h.provider === "youtube");
      ytHits.forEach(h => {
        const li = document.createElement("li");
        li.innerHTML = "<button class='btn btn-secondary btn-sm'>▶</button> <strong></strong> <small></small>";
        li.querySelector("strong").textContent = h.title;
        li.querySelector("small").textContent = (h.artist || "") + " · " + fmt(h.durationSeconds);
        li.querySelector("button").onclick = async () => {
          this.items.splice(this.idx + 1, 0, { title: h.title, videoId: h.videoId });
          await this.playAt(this.idx + 1);
        };
        box.appendChild(li);
      });
      this.setStatus(ytHits.length ? ytHits.length + " resultados." : "Sin resultados.");
    },

    prev() { if (this.idx > 0) this.playAt(this.idx - 1).catch(e => this.setStatus("Error: " + e.message)); },
    next() {
      if (this.idx + 1 < this.items.length)
        this.playAt(this.idx + 1).catch(e => this.setStatus("Error: " + e.message));
    },
    setStatus(t) { if (this.statusEl) this.statusEl.textContent = t; }
  };
})();
