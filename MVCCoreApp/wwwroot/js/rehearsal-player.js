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
    loopA: null, loopB: null, loopTrack: false, videoId: null,

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

      const speed = document.getElementById("rp-speed");
      if (speed) speed.onchange = () => this.applySpeed();
      const back10 = document.getElementById("rp-back10");
      if (back10) back10.onclick = () => this.nudge(-10);
      const fwd10 = document.getElementById("rp-fwd10");
      if (fwd10) fwd10.onclick = () => this.nudge(10);
      const loopTrack = document.getElementById("rp-loop-track");
      if (loopTrack) loopTrack.onclick = () => {
        this.loopTrack = !this.loopTrack;
        try { if (this.player) this.player.setLoop(this.loopTrack); } catch { /* aún no listo */ }
        loopTrack.classList.toggle("btn-primary", this.loopTrack);
        loopTrack.classList.toggle("btn-secondary", !this.loopTrack);
      };
      const setA = document.getElementById("rp-set-a");
      if (setA) setA.onclick = () => this.setLoopPoint("A");
      const setB = document.getElementById("rp-set-b");
      if (setB) setB.onclick = () => this.setLoopPoint("B");
      const clearAb = document.getElementById("rp-clear-ab");
      if (clearAb) clearAb.onclick = () => { this.loopA = this.loopB = null; this.renderAb(); };
      const markAdd = document.getElementById("rp-mark-add");
      if (markAdd) markAdd.onclick = () => this.addMark();

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
      this.videoId = videoId;
      this.loopA = this.loopB = null;
      this.renderAb();
      this.renderMarks();
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
                this.applySpeed();
                try { e.target.setLoop(this.loopTrack); } catch { /* opcional */ }
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
          // Loop A–B de la sección.
          if (this.loopA != null && this.loopB != null && this.loopB > this.loopA && c >= this.loopB)
            this.player.seekTo(this.loopA, true);
          const seek = document.getElementById("rp-seek");
          const t = document.getElementById("rp-time");
          if (seek && d) seek.value = c / d;
          if (t) t.textContent = fmt(c) + " / " + fmt(d);
          this.setPlayIcon(this.player.getPlayerState() === 1);
        } catch { /* aún no listo */ }
      }, 500);
    },

    stopTick() { if (this.tickTimer) { clearInterval(this.tickTimer); this.tickTimer = null; } },

    applySpeed() {
      if (!this.player) return;
      const sel = document.getElementById("rp-speed");
      const v = sel ? Number(sel.value) : 1;
      try { this.player.setPlaybackRate(v); } catch { /* aún no listo */ }
    },

    nudge(sec) {
      if (!this.player) return;
      try {
        const c = this.player.getCurrentTime() || 0;
        const d = this.player.getDuration() || 0;
        this.player.seekTo(Math.min(Math.max(0, c + sec), d), true);
      } catch { /* aún no listo */ }
    },

    now() {
      try { return this.player ? (this.player.getCurrentTime() || 0) : 0; }
      catch { return 0; }
    },

    setLoopPoint(which) {
      const c = this.now();
      if (which === "A") this.loopA = c;
      else this.loopB = c;
      if (this.loopA != null && this.loopB != null && this.loopB <= this.loopA) {
        if (which === "A") this.loopB = null; else this.loopA = null;
        this.setStatus("B debe ser mayor que A; ajusta el otro punto.");
      }
      this.renderAb();
    },

    renderAb() {
      const el = document.getElementById("rp-ab-label");
      if (el) el.textContent = "A–B: " + (this.loopA != null ? fmt(this.loopA) : "—") +
        " → " + (this.loopB != null ? fmt(this.loopB) : "—");
    },

    marksKey() { return "rp-marks-" + (this.videoId || "none"); },

    getMarks() {
      try { return JSON.parse(localStorage.getItem(this.marksKey()) || "[]"); }
      catch { return []; }
    },

    addMark() {
      const input = document.getElementById("rp-mark-label");
      const label = (input ? input.value : "").trim() || ("Sección " + (this.getMarks().length + 1));
      const marks = this.getMarks();
      marks.push({ t: Math.round(this.now()), label });
      marks.sort((a, b) => a.t - b.t);
      try { localStorage.setItem(this.marksKey(), JSON.stringify(marks)); } catch { /* sin storage */ }
      if (input) input.value = "";
      this.renderMarks();
    },

    renderMarks() {
      const box = document.getElementById("rp-marks");
      if (!box) return;
      const marks = this.getMarks();
      box.innerHTML = "";
      marks.forEach((m, i) => {
        const li = document.createElement("li");
        li.className = "rp-item";
        li.innerHTML = "<button class='btn btn-secondary btn-sm'>🚩 " + fmt(m.t) + "</button> <strong></strong> <button class='btn btn-secondary btn-sm' title='Quitar'>✕</button>";
        li.querySelector("strong").textContent = m.label;
        const btns = li.querySelectorAll("button");
        btns[0].onclick = () => { try { this.player.seekTo(m.t, true); this.player.playVideo(); } catch { /* aún no listo */ } };
        btns[1].onclick = () => {
          const rest = this.getMarks().filter((_, j) => j !== i);
          try { localStorage.setItem(this.marksKey(), JSON.stringify(rest)); } catch { /* sin storage */ }
          this.renderMarks();
        };
        box.appendChild(li);
      });
    },

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
