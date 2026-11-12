// Editor en vivo del sitio. Corre dentro del iframe /admin/sitio/lienzo y habla con la barra del panel.
(function(){
  if (window.__EDITOR_SITIO__) return;
  window.__EDITOR_SITIO__ = true;

  const TEXTOS = {
    '#scrollHint':'hero.scrollHint',
    '#showKicker':'show.kicker', '#showTitle':'show.title',
    '#showsKicker':'shows.kicker', '#showsTitle':'shows.title', '#showsNote':'shows.note',
    '#bandKicker':'band.kicker',
    '#blogKicker':'blog.kicker', '#blogTitle':'blog.title',
    '#nlKicker':'nl.kicker', '#nlTitle':'nl.title', '#nlSub':'nl.sub',
    '#musicKicker':'music.kicker', '#musicTitle':'music.title',
    '#spotifyProfileLabel':'music.profileLabel', '#spotifyProfileName':'music.profileName',
    '#musicAlsoIn':'music.alsoIn',
    '#contactKicker':'contact.kicker', '#contactTitle':'contact.title',
    '#contactHl':'contact.hl', '#contactCity':'contact.city',
    '#footBand':'foot.band', '#footCity':'foot.city', '#footLegal':'foot.legal'
  };

  const AJUSTES = {
    '.hero-eyebrow':'HeroEyebrow',
    '.slogan':'Slogan',
    '#band h2':'BandTitle',
    '#bandDesc':'BandDescription'
  };

  // Título partido en dos campos por un <span>.
  const PARTIDOS = [
    ['#hero h1','HeroTitle','HeroTitleAccent'],
    ['#band .highlight','BandHighlight','BandHighlightSub']
  ];

  const SECCIONES = [
    ['next-show','Próximo show','NextShowVisible'],
    ['band','Banda','BandVisible'],
    ['dates','Shows','ShowsVisible'],
    ['blog','Noticias','BlogVisible'],
    ['music','Música','MusicVisible'],
    ['contact','Contacto','ContactVisible'],
    ['newsletter','Newsletter','NewsletterVisible']
  ];

  const cambios = { textos:{}, ajustes:{}, secciones:{} };
  const sucios = new Set();
  let token = null;

  const avisar = () => parent.postMessage({ tipo:'cambios', n: sucios.size }, location.origin);
  const marcar = (clave) => { sucios.add(clave); avisar(); };

  function editar(el, guardar){
    if (el.isContentEditable) return;
    const previo = el.innerHTML;
    el.contentEditable = 'true';
    el.classList.add('editando');
    el.focus();

    const fin = (aceptar)=>{
      el.contentEditable = 'false';
      el.classList.remove('editando');
      el.removeEventListener('blur', alSalir);
      el.removeEventListener('keydown', alTeclear);
      if (!aceptar || el.innerHTML === previo) el.innerHTML = previo;
      else guardar(el);
    };
    const alSalir = ()=> fin(true);
    const alTeclear = (e)=>{
      if (e.key === 'Escape'){ e.preventDefault(); fin(false); }
      else if (e.key === 'Enter' && !el.dataset.multilinea){ e.preventDefault(); el.blur(); }
      else if (e.key === 's' && (e.ctrlKey || e.metaKey)){ e.preventDefault(); fin(true); guardarTodo(); }
    };
    el.addEventListener('blur', alSalir);
    el.addEventListener('keydown', alTeclear);
  }

  function activar(el, guardar){
    if (!el) return;
    el.dataset.editable = '';
    el.addEventListener('click', (e)=>{
      e.preventDefault(); e.stopPropagation();
      editar(el, guardar);
    });
  }

  function guardarTexto(clave){
    return (el)=>{ cambios.textos[clave] = el.innerText.trim(); marcar('t:' + clave); };
  }
  function guardarAjuste(prop){
    return (el)=>{ cambios.ajustes[prop] = el.innerText.trim(); marcar('a:' + prop); };
  }
  function guardarPartido(titulo){
    return (el)=>{
      const copia = el.cloneNode(true);
      const copiaSpan = copia.querySelector('span');
      if (copiaSpan) copiaSpan.remove();
      cambios.ajustes[titulo] = copia.innerText.trim();
      marcar('a:' + titulo);
    };
  }

  // El <span> lo recrea la hidratación del front, así que se resuelve al hacer clic.
  function activarPartido(contenedor, titulo, acento){
    if (!contenedor) return;
    contenedor.dataset.editable = '';
    contenedor.addEventListener('click', (e)=>{
      e.preventDefault(); e.stopPropagation();
      if (e.target.closest('span')) editar(e.target.closest('span'), guardarAjuste(acento));
      else editar(contenedor, guardarPartido(titulo));
    });
  }

  async function guardarTodo(){
    if (!token){ parent.postMessage({ tipo:'error', msg:'Espera un segundo…' }, location.origin); return; }
    try {
      const res = await fetch(location.origin + '/admin/sitio', {
        method:'POST',
        headers:{ 'Content-Type':'application/json', 'X-XSRF-TOKEN': token },
        credentials:'same-origin',
        body: JSON.stringify(cambios)
      });
      if (!res.ok){
        const datos = await res.json().catch(()=>({}));
        parent.postMessage({ tipo:'error', msg: datos.detail || 'No se pudo guardar' }, location.origin);
        return;
      }
      cambios.textos = {}; cambios.ajustes = {}; cambios.secciones = {};
      sucios.clear();
      parent.postMessage({ tipo:'guardado' }, location.origin);
    } catch (e){
      parent.postMessage({ tipo:'error', msg:'Sin conexión con el panel' }, location.origin);
    }
  }

  function montarPanel(){
    const estilo = document.createElement('style');
    estilo.textContent = `
      [data-editable]{ outline:1px dashed rgba(255,255,255,.22); outline-offset:4px; cursor:text; }
      [data-editable]:hover{ outline-color:#fff; }
      [data-editable]:empty{ display:inline-block; min-width:80px; min-height:1em; }
      .editando{ outline:2px solid #fff !important; background:rgba(255,255,255,.06); }
      #panel-editor{ position:fixed; right:16px; bottom:16px; z-index:9999; background:#0d0d0d; border:1px solid rgba(255,255,255,.2);
        border-radius:14px; padding:10px 14px; font:12px/1.6 system-ui; color:#fff; box-shadow:0 16px 40px rgba(0,0,0,.6); }
      #panel-editor summary{ cursor:pointer; font-weight:600; letter-spacing:.02em; }
      #panel-editor label{ display:flex; gap:8px; align-items:center; cursor:pointer; }
      #panel-editor input{ accent-color:#fff; }
      .boton-crud{ position:absolute; z-index:9998; width:26px; height:26px; border-radius:50%; border:1px solid rgba(255,255,255,.35);
        background:rgba(0,0,0,.82); color:#fff; font-size:12px; line-height:1; cursor:pointer; display:grid; place-items:center; }
      .boton-crud:hover{ background:#fff; color:#000; }
      .boton-crud.editar{ top:8px; right:8px; }
      .boton-crud.borrar{ top:8px; right:40px; }
      [data-entidad]{ position:relative; }`;
    document.head.appendChild(estilo);

    const panel = document.createElement('details');
    panel.id = 'panel-editor';
    panel.innerHTML = '<summary>Secciones</summary>' + SECCIONES.map(([id, etiqueta, prop])=>{
      const visible = !document.getElementById(id)?.hidden;
      return `<label><input type="checkbox" data-seccion="${id}" data-prop="${prop}" ${visible ? 'checked' : ''}/> ${etiqueta}</label>`;
    }).join('') + '<div style="margin-top:8px;display:grid;gap:6px">'
      + [['members','＋ Integrante'],['shows','＋ Show'],['news','＋ Noticia'],['tracks','＋ Canción']]
        .map(([ent, txt])=>`<button data-nuevo="${ent}" style="padding:8px;border-radius:10px;border:1px dashed rgba(255,255,255,.4);background:transparent;color:#fff;cursor:pointer">${txt}</button>`).join('')
      + '</div>';
    document.body.appendChild(panel);

    panel.addEventListener('change', (e)=>{
      const casilla = e.target;
      if (!casilla.dataset.seccion) return;
      const seccion = document.getElementById(casilla.dataset.seccion);
      if (seccion) seccion.hidden = !casilla.checked;
      cambios.secciones[casilla.dataset.prop] = casilla.checked;
      marcar('s:' + casilla.dataset.prop);
    });

    // El front publicado puede ir por detrás: el estado de las secciones lo manda Mongo.
    fetch(location.origin + '/api/v1/home').then((r)=> r.ok ? r.json() : null).then((datos)=>{
      const ajustes = datos && datos.settings;
      if (!ajustes) return;
      SECCIONES.forEach(([id, , prop])=>{
        const clave = prop[0].toLowerCase() + prop.slice(1);
        if (ajustes[clave] === undefined) return;
        const seccion = document.getElementById(id);
        if (seccion) seccion.hidden = ajustes[clave] === false;
        const casilla = panel.querySelector(`input[data-prop="${prop}"]`);
        if (casilla) casilla.checked = ajustes[clave] !== false;
      });
    }).catch(()=>{});
  }

  // CRUD embebido: lápiz y papelera sobre cada tarjeta/fila con data-entidad+data-id.
  // La hidratación del front reemplaza el HTML, así que se reconecta con un observador.
  function ponerBotones(nodo){
    if (!nodo.dataset || !nodo.dataset.entidad || !nodo.dataset.id) return;
    if (nodo.querySelector(':scope > .boton-crud')) return;
    const editar = document.createElement('button');
    editar.className = 'boton-crud editar'; editar.title = 'Editar'; editar.textContent = '✎';
    const borrar = document.createElement('button');
    borrar.className = 'boton-crud borrar'; borrar.title = 'Eliminar'; borrar.textContent = '🗑';
    const abrir = (accion)=> (e)=>{
      e.preventDefault(); e.stopPropagation();
      parent.postMessage({ tipo:'crud', entidad:nodo.dataset.entidad, accion, id:nodo.dataset.id }, location.origin);
    };
    editar.addEventListener('click', abrir('editar'));
    borrar.addEventListener('click', abrir('borrar'));
    nodo.appendChild(editar); nodo.appendChild(borrar);
  }

  function conectarCrud(raiz){
    (raiz || document).querySelectorAll('[data-entidad][data-id]').forEach(ponerBotones);
  }

  new MutationObserver((cambios)=>{
    cambios.forEach((c)=> c.addedNodes.forEach((n)=>{ if (n.nodeType !== 1) return; ponerBotones(n); conectarCrud(n); }));
  }).observe(document.body, { childList:true, subtree:true });

  // En el editor los enlaces y formularios no navegan.
  document.addEventListener('click', (e)=>{ if (e.target.closest('a')) e.preventDefault(); }, true);
  document.addEventListener('submit', (e)=> e.preventDefault(), true);

  window.addEventListener('message', (e)=>{
    if (e.origin !== location.origin || !e.data) return;
    if (e.data.tipo === 'token') token = e.data.token;
    if (e.data.tipo === 'guardar') guardarTodo();
  });

  Object.entries(TEXTOS).forEach(([sel, clave])=> activar(document.querySelector(sel), guardarTexto(clave)));
  Object.entries(AJUSTES).forEach(([sel, prop])=> activar(document.querySelector(sel), guardarAjuste(prop)));
  PARTIDOS.forEach(([sel, titulo, acento])=> activarPartido(document.querySelector(sel), titulo, acento));
  const desc = document.getElementById('bandDesc');
  if (desc) desc.dataset.multilinea = '';

  montarPanel();
  conectarCrud(document);

  // Los botones ＋ del panel viven en el lienzo pero abren el modal del panel.
  document.getElementById('panel-editor').querySelectorAll('[data-nuevo]').forEach((b)=> b.addEventListener('click', ()=>{
    parent.postMessage({ tipo:'crud', entidad:b.dataset.nuevo, accion:'nuevo' }, location.origin);
  }));
})();
