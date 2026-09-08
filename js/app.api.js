// Hidratación dinámica de la home desde el back .NET (Mongo)
// Mantiene fallback estático si API no responde (GitHub Pages vanilla).
(function(){
  const $ = (s, r=document)=>r.querySelector(s);
  const $$ = (s, r=document)=>[...r.querySelectorAll(s)];

  function setText(sel, text){
    const el = $(sel); if(el && text) el.textContent = text;
  }
   function esc(s){ return String(s||'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;').replace(/'/g,'&#39;'); }
  function setHtml(sel, html){
    const el = $(sel); if(el && html) el.innerHTML = html;
  }

  async function hydrate(){
    if(!window.AppAPI) return;
    try {
      const data = await window.AppAPI.getHome();
      console.log('[hydrate] home', data);
      if(!data) return;

      // SETTINGS
      if(data.settings){
        const s = data.settings;
        // SEO
        if(s.seoTitle) document.title = s.seoTitle;
        const desc = document.querySelector('meta[name="description"]');
        if(desc && s.seoDescription) desc.content = s.seoDescription;
        // hero
        setText('.hero-eyebrow', s.heroEyebrow);
        if(s.siteTitle) {
          const h1 = $('#hero h1');
          // siteTitle viene plano, mantenemos split "Tú no eres el rey / del tren fantasma" si coincide
          // fallback: solo reemplazar si h1 existe
        }
        if(s.slogan) setText('.slogan', s.slogan);
        if(s.heroImageUrl){
          const hero = document.getElementById('hero');
          if(hero) hero.style.setProperty('--hero-img', `url('${s.heroImageUrl}')`);
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

      // NEXT SHOW (próximo por fecha más cercana)
      if(data.nextShow){
        const n = data.nextShow;
        const d = new Date(n.date);
        const day = d.getUTCDate().toString().padStart(2,'0');
        const month = (d.getUTCMonth()+1).toString().padStart(2,'0');
        const venueEl = $('.next-show-venue');
        const dateEl = $('.next-show-date');
        if(dateEl) dateEl.innerHTML = `${day} <small>/ ${month}</small>`;
        if(venueEl) venueEl.innerHTML = `${n.venue} <span>— ${n.cityState}</span>`;
        // kicker maybe with venue
      }

      // MEMBERS - esc() para evitar XSS en name/role
      if(Array.isArray(data.members) && data.members.length){
        const grid = $('.member-grid');
        if(grid){
          grid.innerHTML = data.members.map(m=>`
            <div class="member-card anim-y showed" data-href="${esc(m.profileUrl)}">
              <div class="img-wrap"><img src="${esc(m.imageUrl)}" alt="${esc(m.imageAlt||m.name)}" loading="lazy"><div class="overlay"><i class="fa-solid fa-arrow-up-right-from-square"></i></div></div>
              <p class="name">${esc(m.name)}</p><p class="role">${esc(m.role)}</p>
            </div>
          `).join('');
          // rebind click
          grid.querySelectorAll('.member-card').forEach(c=> c.addEventListener('click', ()=> window.open(c.dataset.href||'https://www.instagram.com/','_blank')));
        }
      }

      // SHOWS - esc venue/cityState/time
      if(Array.isArray(data.shows)){
        const tbody = document.querySelector('.shows-table tbody');
        if(tbody){
          tbody.innerHTML = data.shows.filter(s=>!s.isDeleted && s.isVisible).map(s=>{
            const d = new Date(s.date);
            const dd = d.getUTCDate().toString().padStart(2,'0');
            const mm = (d.getUTCMonth()+1).toString().padStart(2,'0');
            const yyyy = d.getUTCFullYear();
            const mapsAttr = s.googleMapsUrl ? `style="cursor:pointer" data-maps="${esc(s.googleMapsUrl)}" title="Ver en Google Maps"` : '';
            return `<tr class="anim-y showed" ${mapsAttr}><td>${dd} / ${mm} / ${yyyy}</td><td><span class="venue">${esc(s.venue)} <small>${esc(s.cityState)}</small></span></td><td>${esc(s.time)}</td></tr>`;
          }).join('');
          tbody.querySelectorAll('tr[data-maps]').forEach(tr=>{
            tr.addEventListener('click', ()=> window.open(tr.dataset.maps,'_blank'));
          });
        }
      }

      // NEWS - esc title/tag
      if(Array.isArray(data.news)){
        const grid = $('.blog-grid');
        if(grid){
          grid.innerHTML = data.news.filter(n=>n.isPublished).map(n=>`
            <article class="blog-card anim-y showed"><div class="img-wrap"><img class="blog-img" src="${esc(n.imageUrl)}" alt="${esc(n.imageAlt||n.title)}" loading="lazy"></div><div class="blog-body"><p class="blog-tag"><i class="fa-solid fa-newspaper" style="font-size:9px;"></i> ${esc(n.tag)}</p><h3 class="blog-title">${esc(n.title)}</h3><p class="blog-date"><i class="fa-regular fa-calendar" style="margin-right:6px;"></i>${new Date(n.publishedAt).toLocaleDateString('es-MX')} — ${esc(n.tag)}</p></div></article>
          `).join('');
        }
      }

      // TRACKS
      if(Array.isArray(data.tracks)){
        const track = data.tracks.filter(t=>t.isVisible!==false).sort((a,b)=>a.trackNumber-b.trackNumber);
        // carrusel
        const carouselTrack = $('#carouselTrack');
        const countEl = $('#carouselCount');
        if(carouselTrack && track.length){
          carouselTrack.innerHTML = track.map(t=>`
            <div class="carousel-slide">
              <div class="spotify-card">
                <div class="spotify-head"><span class="spotify-badge">${String(t.trackNumber).padStart(2,'0')}</span><span class="spotify-title">${t.title}</span></div>
                ${t.links.spotifyEmbedUrl ? `<iframe style="border-radius:12px" src="${t.links.spotifyEmbedUrl}" width="100%" height="352" frameborder="0" allow="autoplay; clipboard-write; encrypted-media; fullscreen; picture-in-picture" loading="lazy" title="${t.title}"></iframe>` : (t.links.youtubeUrl ? `<div style="padding:12px"><a href="${t.links.youtubeUrl}" target="_blank" rel="noopener"><i class="fa-brands fa-youtube"></i> Ver en YouTube</a></div>` : '')}
                <div class="carousel-actions"><a href="${t.links.spotifyUrl||t.links.youtubeUrl||'#'}" target="_blank" rel="noopener"><i class="fa-brands fa-spotify"></i> Abrir</a></div>
              </div>
            </div>
          `).join('');
          if(countEl) countEl.textContent = `1 / ${track.length}`;
          // re-inicializar carrusel con la nueva data (dots, flechas, scroll)
          if(window.initCarousel) setTimeout(window.initCarousel, 50);
        }
        // stream grid - esc title/type, links ya son URLs validadas por Cloudinary
        const streamGrid = $('.stream-grid');
        if(streamGrid){
          streamGrid.innerHTML = track.map(t=>`
            <li><span class="stream-num">${String(t.trackNumber).padStart(2,'0')}</span><strong class="stream-title">${esc(t.title)}</strong><span class="stream-meta">${esc(t.type)}</span><span class="stream-platforms">
              ${t.links.spotifyUrl?`<a href="${esc(t.links.spotifyUrl)}" target="_blank" rel="noopener"><i class="fa-brands fa-spotify"></i> Spotify</a>`:''}
              ${t.links.appleMusicUrl?`<a href="${esc(t.links.appleMusicUrl)}" target="_blank" rel="noopener"><i class="fa-brands fa-apple"></i> Apple</a>`:''}
              ${t.links.youtubeUrl?`<a href="${esc(t.links.youtubeUrl)}" target="_blank" rel="noopener"><i class="fa-brands fa-youtube"></i> YouTube</a>`:''}
            </span></li>
          `).join('');
          if(track.length===0) streamGrid.innerHTML='<li style="opacity:.6">No hay tracks aún</li>';
        }
        // actualizar texto "5 singles"
        const singlesText = document.querySelector('#music p.anim-y');
        if(singlesText && track.length) singlesText.textContent = `${track.length} singles — dale play sin salir de aquí. Desliza o usa las flechas.`;
      }

    } catch(e){
      console.warn('[hydrate] fallo, usando contenido estático', e);
    }
  }

  // Espera a DOM + config
  if(document.readyState==='loading') document.addEventListener('DOMContentLoaded', hydrate);
  else hydrate();
})();
