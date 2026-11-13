// Hidratación dinámica de la home desde el back .NET (Mongo)
// Mantiene fallback estático si API no responde. Usa window.DomUtils si existe.
(function(){
  const D = window.DomUtils || {};
  const $ = D.$ || ((s, r=document)=>r.querySelector(s));
  const $$ = D.$$ || ((s, r=document)=>[...r.querySelectorAll(s)]);

  function setText(sel, text){
    if(D.setText) return D.setText(sel, text);
    const el = $(sel); if(el && text) el.textContent = text;
  }
   function esc(s){ if(D.esc) return D.esc(s); return String(s||'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;').replace(/'/g,'&#39;'); }
  // shortcut: embed Spotify sale solo del link (track/album/playlist/episodio)
  function spotifyEmbed(u){ const m = String(u||'').match(/open\.spotify\.com\/(?:intl-[a-z-]+\/)?(track|album|playlist|episode)\/([A-Za-z0-9]+)/i); return m ? `https://open.spotify.com/embed/${m[1]}/${m[2]}?utm_source=generator&theme=0` : null; }
  function setHtml(sel, html){
    if(D.setHtml) return D.setHtml(sel, html);
    const el = $(sel); if(el && html) el.innerHTML = html;
  }

  async function hydrate(){
    if(!window.AppAPI) return;
    try {
      const data = await window.AppAPI.getHome();
      console.log('[hydrate] home', data);
      if(!data) return;
      const T = data.texts || {};

      // SETTINGS
      if(data.settings){
        const s = data.settings;
        // SEO
        if(s.seoTitle || s.siteTitle) document.title = s.seoTitle || s.siteTitle;
        const desc = document.querySelector('meta[name="description"]');
        if(desc && s.seoDescription) desc.content = s.seoDescription;
        // Open Graph
        const og = (id, val)=>{ const el=document.getElementById(id); if(el && val) el.content = val; };
        og('ogTitle', s.seoTitle || s.siteTitle);
        og('ogDesc', s.seoDescription);
        const ogImgEl = document.getElementById('ogImage'); if(ogImgEl) ogImgEl.content = new URL(s.heroImageUrl || ogImgEl.content, location.href).href;
        // secciones visibles
        const secs = [
          ['next-show', s.nextShowVisible], ['band', s.bandVisible], ['dates', s.showsVisible],
          ['blog', s.blogVisible], ['newsletter', s.newsletterVisible], ['music', s.musicVisible],
          ['contact', s.contactVisible]
        ];
        secs.forEach(([id, visible])=>{ const el=document.getElementById(id); if(el && visible === false) el.hidden = true; else if(el) el.hidden = false; });
        // hero
        setText('.hero-eyebrow', s.heroEyebrow);
        if(s.heroTitle || s.heroTitleAccent) {
          const h1 = $('#hero h1');
          if(h1) h1.innerHTML = `${esc(s.heroTitle)} <span>${esc(s.heroTitleAccent)}</span>`;
        }
        if(s.slogan) setText('.slogan', s.slogan);
        if(s.heroImageUrl){
          const hero = document.getElementById('hero');
          if(hero) {
            hero.style.setProperty('--hero-img', `url('${s.heroImageUrl}')`);
            if(s.heroImageZoom) hero.style.setProperty('--hero-zoom', (s.heroImageZoom / 100).toFixed(2));
            if(s.heroImagePosX !== undefined) hero.style.setProperty('--hero-x', `${s.heroImagePosX}%`);
            if(s.heroImagePosY !== undefined) hero.style.setProperty('--hero-y', `${s.heroImagePosY}%`);
          }
        }
        if(s.contactEmail){
          const insertMail = $('#insertMail');
          if(insertMail) insertMail.innerHTML = `<a href="mailto:${esc(s.contactEmail)}">${esc(s.contactEmail)}</a>`;
        }
        // redes: 1 lista -> filtra ShowInHero / ShowInFooter
        if(Array.isArray(s.socialLinks)){
          const heroLinks = s.socialLinks.filter(l=>l.isActive && l.showInHero).sort((a,b)=>a.order-b.order);
          const footerLinks = s.socialLinks.filter(l=>l.isActive && l.showInFooter).sort((a,b)=>a.order-b.order);
          const heroContainer = $('.share-btn');
          const footerContainer = $('.footer-socials');
          const render = (links)=>links.map(l=>`<a href="${l.url}" class="social-icon" aria-label="${l.platform}" target="_blank" rel="noopener"><i class="${l.iconClass}"></i></a>`).join('');
          if(heroContainer && heroLinks.length) heroContainer.innerHTML = render(heroLinks);
          if(footerContainer && footerLinks.length) footerContainer.innerHTML = render(footerLinks);
          // youtube / spotify profile link
          const spotifyLink = document.querySelector('.spotify-profile-link');
          const spotifyUrl = s.socialLinks.find(l=>l.platform===2)?.url || s.spotifyArtistUrl;
          if(spotifyLink && spotifyUrl) spotifyLink.href = spotifyUrl;
        }
        // band
        if(s.bandTitle) setText('#band h2', s.bandTitle);
        if(s.bandHighlight) {
          const hl = document.querySelector('#band .highlight');
          if(hl) hl.innerHTML = `${s.bandHighlight} <span>${s.bandHighlightSub||''}</span>`;
        }
        if(s.bandDescription){
          const descEl = document.getElementById('bandDesc') || document.querySelector('#band .intro p#bandDesc');
          if(descEl) descEl.innerHTML = s.bandDescription;
        }
      }

      // HERO CHIPS (sellos bajo el slogan, colección heroChips)
      if(Array.isArray(data.heroChips)){
        const vis = data.heroChips.filter(c=>c.isVisible).sort((a,b)=>a.order-b.order);
        const meta = $('.hero-meta');
        if(meta) meta.innerHTML = vis.map(c=>`<span><i class="${esc(c.icon)}" style="color:var(--text);margin-right:6px;opacity:.9;"></i> ${esc(c.text)}</span>`).join('');
      }

      // TEXTOS DEL SITIO (clave-valor por grupos, colección siteTexts)
      if(data.texts && typeof data.texts === 'object'){
        const T = data.texts;
        window.__SITE_TEXTS__ = T;
        const val = (k)=> (T[k]!==undefined && T[k]!==null && String(T[k]).trim()!=='') ? String(T[k]) : null;
        const setT = (id, k)=>{ const v=val(k); if(v===null) return; const el=document.getElementById(id); if(el) el.textContent=v; };
        const setPh = (id, k)=>{ const v=val(k); if(v===null) return; const el=document.getElementById(id); if(el) el.placeholder=v; };
        setT('scrollHint','hero.scrollHint');
        setT('showKicker','show.kicker'); setT('showTitle','show.title');
        setT('bandKicker','band.kicker');
        setT('showsKicker','shows.kicker'); setT('showsTitle','shows.title'); setT('showsNote','shows.note');
        setT('blogKicker','blog.kicker'); setT('blogTitle','blog.title');
        setT('nlKicker','nl.kicker'); setT('nlTitle','nl.title'); setT('nlSub','nl.sub');
        setT('musicKicker','music.kicker'); setT('musicTitle','music.title');
        setT('spotifyProfileName','music.profileName');
        setT('spotifyProfileLabel','music.profileLabel');
        // navegación lateral + accesibilidad del carrusel
        $$('[data-text]').forEach(el=>{ const v=val(el.dataset.text); if(v) el.title=v; });
        const cp=val('carousel.prev'), cn=val('carousel.next');
        if(cp){ const b=document.getElementById('carouselPrev'); if(b) b.setAttribute('aria-label',cp); }
        if(cn){ const b=document.getElementById('carouselNext'); if(b) b.setAttribute('aria-label',cn); }
        setT('contactKicker','contact.kicker'); setT('contactTitle','contact.title');
        setT('contactHl','contact.hl'); setT('contactCity','contact.city');
        setT('footBand','foot.band'); setT('footCity','foot.city'); setT('footLegal','foot.legal');
        // textos con icono
        const live = val('show.liveLabel');
        if(live){ const el=$('.next-show-kicker'); if(el) el.innerHTML=`<i class="fa-solid fa-circle" style="font-size:6px; color:var(--accent);"></i> ${esc(live)}`; }
        const cta = val('show.cta');
        if(cta){ const el=$('.cta-link'); if(el) el.innerHTML=`${esc(cta)} <i class="fa-solid fa-arrow-right"></i>`; }
        const also = val('music.alsoIn');
        if(also){ const el=document.getElementById('musicAlsoIn'); if(el) el.innerHTML=`<i class="fa-solid fa-list"></i> ${esc(also)}`; }
        const nlBtn = val('nl.button');
        if(nlBtn){ const b=document.getElementById('subBtn'); if(b){ b.innerHTML=`<i class="fa-solid fa-paper-plane" style="margin-right:6px;"></i> ${esc(nlBtn)}`; b.dataset.label=nlBtn; } }
        const cBtn = val('contact.submit');
        if(cBtn){ const b=document.getElementById('contactSubmit'); if(b) b.innerHTML=`<i class="fa-solid fa-paper-plane" style="margin-right:8px;"></i> ${esc(cBtn)}`; }
        // placeholders + anti-robot
        setPh('emailNews','nl.placeholder');
        setPh('nom','contact.namePh'); setPh('mail','contact.mailPh');
        setPh('telephone','contact.telPh'); setPh('message','contact.msgPh');
      }

      // NEXT SHOW (próximo por fecha más cercana)
      const vacio = T['site.empty'] || 'Aún muy fantasmal por aquí…';
      const card = $('.next-show-card');
      const sinShow = document.getElementById('nextShowEmpty');
      if(sinShow) sinShow.textContent = vacio;
      if(data.nextShow){
        const n = data.nextShow;
        const d = new Date(n.date);
        const day = d.getUTCDate().toString().padStart(2,'0');
        const month = (d.getUTCMonth()+1).toString().padStart(2,'0');
        const venueEl = $('.next-show-venue');
        const dateEl = $('.next-show-date');
        if(dateEl) dateEl.innerHTML = `${day} <small>/ ${month}</small>`;
        if(venueEl) venueEl.innerHTML = `${esc(n.venue)} <span>— ${esc(n.cityState)}</span>`;
        if(card) card.hidden = false;
        if(sinShow) sinShow.hidden = true;
      } else if(card){
        card.hidden = true;
        if(sinShow) sinShow.hidden = false;
      }

      // MEMBERS - esc() para evitar XSS en name/role
      if(Array.isArray(data.members)){
        const grid = $('.member-grid');
        if(grid){
          if(!data.members.length){
            grid.innerHTML = `<p class="empty-ghost">${esc(vacio)}</p>`;
          } else {
          grid.innerHTML = data.members.map(m=>`
            <div class="member-card anim-y showed" data-entidad="members" data-id="${esc(m.id)}" data-href="${esc(m.profileUrl)}">
              <div class="img-wrap"><img src="${esc(m.imageUrl)}" alt="${esc(m.imageAlt||m.name)}" loading="lazy"><div class="overlay"><i class="fa-solid fa-arrow-up-right-from-square"></i></div></div>
              <p class="name">${esc(m.name)}</p><p class="role">${esc(m.role)}</p>
            </div>
          `).join('');
          // rebind click
          grid.querySelectorAll('.member-card').forEach(c=> c.addEventListener('click', ()=> window.open(c.dataset.href||'https://www.instagram.com/','_blank')));
          }
        }
      }

      // SHOWS - esc venue/cityState/time
      if(Array.isArray(data.shows)){
        const tbody = document.querySelector('.shows-table tbody');
        if(tbody){
          const vis = data.shows.filter(s=>!s.isDeleted && s.isVisible);
          tbody.innerHTML = vis.length ? vis.map(s=>{
            const d = new Date(s.date);
            const dd = d.getUTCDate().toString().padStart(2,'0');
            const mm = (d.getUTCMonth()+1).toString().padStart(2,'0');
            const yyyy = d.getUTCFullYear();
            const mapsAttr = s.googleMapsUrl ? `style="cursor:pointer" data-maps="${esc(s.googleMapsUrl)}" title="Ver en Google Maps"` : '';
            return `<tr class="anim-y showed" data-entidad="shows" data-id="${esc(s.id)}" ${mapsAttr}><td>${dd} / ${mm} / ${yyyy}</td><td><span class="venue">${esc(s.venue)} <small>${esc(s.cityState)}</small></span></td><td>${esc(s.time)}</td></tr>`;
          }).join('') : `<tr><td colspan="3" class="empty-ghost">${esc(vacio)}</td></tr>`;
          tbody.querySelectorAll('tr[data-maps]').forEach(tr=>{
            tr.addEventListener('click', ()=> window.open(tr.dataset.maps,'_blank'));
          });
        }
      }

      // NEWS - la tarjeta abre modal con el detalle completo
      let noticias = [];
      const abrirNoticia = (id)=>{
        const n = noticias.find(x=>String(x.id)===String(id));
        if(!n) return;
        const modal = $('#noticiaModal');
        if(!modal) return;
        const img = $('#noticiaImg');
        if(img){ img.src = n.imageUrl || ''; img.alt = n.imageAlt || n.title || ''; img.closest('.img-wrap').style.display = n.imageUrl ? '' : 'none'; }
        const tag = $('#noticiaTag'); if(tag){ tag.innerHTML = `<i class="fa-solid fa-newspaper" style="font-size:9px;"></i> ${esc(n.tag)}`; tag.style.display = n.tag ? '' : 'none'; }
        const tit = $('#noticiaTitulo'); if(tit) tit.textContent = n.title || '';
        const fec = $('#noticiaFecha'); if(fec) fec.innerHTML = `<i class="fa-regular fa-calendar" style="margin-right:6px;"></i>${new Date(n.publishedAt).toLocaleDateString('es-MX')}`;
        const cuerpo = $('#noticiaCuerpo'); if(cuerpo) cuerpo.innerHTML = n.contentHtml || '';
        modal.hidden = false;
        document.body.style.overflow = 'hidden';
      };
      const cerrarNoticia = ()=>{ const modal = $('#noticiaModal'); if(modal) modal.hidden = true; document.body.style.overflow = ''; };
      document.addEventListener('click', (e)=>{
        const btn = e.target.closest('[data-noticia]');
        if(btn){ abrirNoticia(btn.dataset.noticia); return; }
        if(e.target.closest('#noticiaCerrar') || e.target.id === 'noticiaModal') cerrarNoticia();
      });
      document.addEventListener('keydown', (e)=>{ if(e.key === 'Escape') cerrarNoticia(); });
      if(Array.isArray(data.news)){
        const grid = $('.blog-grid');
        if(grid){
          const pub = data.news.filter(n=>n.isPublished);
          noticias = pub;
          grid.innerHTML = pub.length ? pub.map(n=>`
            <article class="blog-card anim-y showed" data-entidad="news" data-id="${esc(n.id)}"><div class="img-wrap"><img class="blog-img" src="${esc(n.imageUrl)}" alt="${esc(n.imageAlt||n.title)}" loading="lazy"></div><div class="blog-body"><p class="blog-tag"><i class="fa-solid fa-newspaper" style="font-size:9px;"></i> ${esc(n.tag)}</p><h3 class="blog-title">${esc(n.title)}</h3><p class="blog-date"><i class="fa-regular fa-calendar" style="margin-right:6px;"></i>${new Date(n.publishedAt).toLocaleDateString('es-MX')} — ${esc(n.tag)}</p>${n.excerpt?`<p class="blog-excerpt">${esc(n.excerpt)}</p>`:''}${n.contentHtml?`<button class="blog-more-btn" data-noticia="${esc(n.id)}">Leer noticia</button>`:''}</div></article>
          `).join('') : `<p class="empty-ghost">${esc(vacio)}</p>`;
        }
      }

      // TRACKS
      if(Array.isArray(data.tracks)){
        const track = data.tracks.filter(t=>t.isVisible!==false).sort((a,b)=>a.trackNumber-b.trackNumber);
        // carrusel
        const carouselTrack = $('#carouselTrack');
        const countEl = $('#carouselCount');
        const carouselWrap = $('#songCarousel');
        const sinTracks = track.length === 0;
        if(carouselWrap) carouselWrap.hidden = sinTracks;
        const dotsEl = document.getElementById('carouselDots');
        if(dotsEl) dotsEl.hidden = sinTracks;
        if(countEl) countEl.hidden = sinTracks;
        if(carouselTrack && track.length){
          carouselTrack.innerHTML = track.map(t=>{
            const embed = spotifyEmbed(t.links.spotifyUrl);
            return `
            <div class="carousel-slide" data-entidad="tracks" data-id="${esc(t.id)}">
              <div class="spotify-card">
                <div class="spotify-head"><span class="spotify-badge">${String(t.trackNumber).padStart(2,'0')}</span><span class="spotify-title">${esc(t.title)}</span></div>
                ${embed ? `<iframe style="border-radius:12px" src="${embed}" width="100%" height="352" frameborder="0" allow="autoplay; clipboard-write; encrypted-media; fullscreen; picture-in-picture" loading="lazy" title="${esc(t.title)}"></iframe>` : (t.links.youtubeUrl ? `<div style="padding:12px"><a href="${esc(t.links.youtubeUrl)}" target="_blank" rel="noopener"><i class="fa-brands fa-youtube"></i> Ver en YouTube</a></div>` : '')}
                <div class="carousel-actions"><a href="${esc(t.links.spotifyUrl||t.links.youtubeUrl||'#')}" target="_blank" rel="noopener"><i class="fa-brands fa-spotify"></i> Abrir</a></div>
              </div>
            </div>
          `;}).join('');
          if(countEl) countEl.textContent = `1 / ${track.length}`;
          // re-inicializar carrusel con la nueva data (dots, flechas, scroll)
          if(window.initCarousel) setTimeout(window.initCarousel, 50);
        }
        // stream grid - esc title/type, links ya son URLs validadas por Cloudinary
        const streamGrid = $('.stream-grid');
        if(streamGrid){
          streamGrid.innerHTML = track.map(t=>`
            <li><span class="stream-num">${String(t.trackNumber).padStart(2,'0')}</span><strong class="stream-title">${esc(t.title)}</strong><span class="stream-meta">${esc(t.type)}</span><span class="stream-platforms">
              ${t.links.spotifyUrl?`<a href="${esc(t.links.spotifyUrl)}" target="_blank" rel="noopener"><i class="fa-brands fa-spotify"></i> ${esc(T['music.platformSpotify']||'Spotify')}</a>`:''}
              ${t.links.appleMusicUrl?`<a href="${esc(t.links.appleMusicUrl)}" target="_blank" rel="noopener"><i class="fa-brands fa-apple"></i> ${esc(T['music.platformApple']||'Apple')}</a>`:''}
              ${t.links.youtubeUrl?`<a href="${esc(t.links.youtubeUrl)}" target="_blank" rel="noopener"><i class="fa-brands fa-youtube"></i> ${esc(T['music.platformYouTube']||'YouTube')}</a>`:''}
            </span></li>
          `).join('');
          if(sinTracks) streamGrid.innerHTML=`<p class="empty-ghost">${esc(vacio)}</p>`;
        }
        // actualizar texto "N singles" (usa music.introSuffix si la API lo trae)
        const singlesText = document.getElementById('musicIntro');
        const alsoInEl = document.getElementById('musicAlsoIn');
        if(singlesText && sinTracks) singlesText.hidden = true;
        if(alsoInEl && sinTracks) alsoInEl.hidden = true;
        if(singlesText && track.length){
          const suffix = T['music.introSuffix'] || 'singles — dale play sin salir de aquí. Desliza o usa las flechas.';
          singlesText.classList.remove('skel');
          singlesText.textContent = `${track.length} ${suffix}`;
        }
      }

    } catch(e){
      console.warn('[hydrate] fallo, sin datos del back', e);
      const vacio = 'Aún muy fantasmal por aquí…';
      const llena = (sel, html)=>{ const el=$(sel); if(el && el.querySelector('.skel,.spin')) el.innerHTML = html; };
      llena('.member-grid', `<p class="empty-ghost">${esc(vacio)}</p>`);
      llena('.shows-table tbody', `<tr><td colspan="3" class="empty-ghost">${esc(vacio)}</td></tr>`);
      llena('.blog-grid', `<p class="empty-ghost">${esc(vacio)}</p>`);
      llena('.stream-grid', `<p class="empty-ghost">${esc(vacio)}</p>`);
      const card = $('.next-show-card');
      if(card){ card.hidden = true; }
      const sinShow = document.getElementById('nextShowEmpty');
      if(sinShow){ sinShow.textContent = vacio; sinShow.hidden = false; }
      const intro = document.getElementById('musicIntro');
      if(intro){ intro.classList.remove('skel'); intro.textContent = vacio; }
      ['songCarousel','carouselDots','carouselCount','musicAlsoIn'].forEach((id)=>{ const el=document.getElementById(id); if(el) el.hidden = true; });
    } finally {
      document.body.classList.add('listo');
    }
  }

  // Espera a DOM + config
  if(document.readyState==='loading') document.addEventListener('DOMContentLoaded', hydrate);
  else hydrate();
})();
