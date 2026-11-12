// CRUD embebido del editor del sitio. El lienzo avisa (postMessage 'crud') y aquí se abre el modal.
(function(){
  const ESQUEMAS = {
    members: { titulo:'Integrante', campos:[
      ['name','Nombre','texto'], ['role','Rol','texto'],
      ['imageUrl','Foto','imagen'], ['imageAlt','Texto alternativo','texto'],
      ['profileUrl','Enlace','texto'], ['order','Orden','numero'], ['isActive','Visible','check']] },
    shows: { titulo:'Show', campos:[
      ['date','Fecha','fecha'], ['time','Hora','texto'],
      ['venue','Lugar','texto'], ['cityState','Ciudad','texto'],
      ['googleMapsUrl','Google Maps','texto'], ['order','Orden','numero'], ['isVisible','Visible','check']] },
    news: { titulo:'Noticia', campos:[
      ['title','Título','texto'], ['slug','Slug','texto'], ['tag','Etiqueta','texto'],
      ['imageUrl','Imagen','imagen'], ['imageAlt','Texto alternativo','texto'],
      ['excerpt','Resumen','area'], ['contentHtml','Contenido','area'],
      ['publishedAt','Publicada','fecha'], ['order','Orden','numero'], ['isPublished','Publicada','check']] },
    tracks: { titulo:'Canción', campos:[
      ['title','Título','texto'], ['trackNumber','Número','numero'], ['type','Tipo','texto'],
      ['links.spotifyUrl','Spotify','texto'], ['links.spotifyEmbedUrl','Spotify (embed)','texto'],
      ['links.appleMusicUrl','Apple Music','texto'], ['links.youTubeUrl','YouTube','texto'],
      ['order','Orden','numero'], ['isVisible','Visible','check']] }
  };

  const modal = document.getElementById('modalCrud');
  const titulo = document.getElementById('crudTitulo');
  const forma = document.getElementById('crudForma');
  const btnGuardar = document.getElementById('crudGuardar');
  const btnBorrar = document.getElementById('crudBorrar');
  let actual = null; // {entidad, id, item}

  const porRuta = (obj, ruta)=> ruta.split('.').reduce((o,k)=> o ? o[k] : undefined, obj);
  const ponRuta = (obj, ruta, valor)=>{ const ks=ruta.split('.'); let o=obj; ks.slice(0,-1).forEach((k)=> o=o[k] ??= {}); o[ks.at(-1)]=valor; };
  const aFecha = (iso)=> iso ? String(iso).slice(0,10) : '';

  function campoHtml([ruta, etiqueta, tipo], valor){
    const id = 'f-' + ruta.replaceAll('.', '-');
    if (tipo === 'check') return `<label class="check"><input id="${id}" type="checkbox" ${valor ? 'checked' : ''}/> ${etiqueta}</label>`;
    if (tipo === 'area') return `<div class="form-group"><label class="form-label">${etiqueta}</label><textarea id="${id}" class="form-control" rows="3">${valor ?? ''}</textarea></div>`;
    if (tipo === 'imagen') return `<div class="form-group"><label class="form-label">${etiqueta}</label><div class="input-row"><input id="${id}" class="form-control" value="${valor ?? ''}"/><button type="button" class="btn btn-secondary btn-sm" onclick="openImagePicker('${id}')"><i class="fa-solid fa-images"></i> Elegir</button></div></div>`;
    if (tipo === 'fecha') return `<div class="form-group"><label class="form-label">${etiqueta}</label><input id="${id}" type="date" class="form-control" value="${aFecha(valor)}"/></div>`;
    if (tipo === 'numero') return `<div class="form-group"><label class="form-label">${etiqueta}</label><input id="${id}" type="number" class="form-control" value="${valor ?? 0}"/></div>`;
    return `<div class="form-group"><label class="form-label">${etiqueta}</label><input id="${id}" class="form-control" value="${(valor ?? '').toString().replaceAll('"','&quot;')}"/></div>`;
  }

  function leer(esquema){
    const datos = {};
    esquema.campos.forEach(([ruta, , tipo])=>{
      const el = document.getElementById('f-' + ruta.replaceAll('.', '-'));
      if (!el) return;
      let v = tipo === 'check' ? el.checked : el.value;
      if (tipo === 'fecha' && v) v = new Date(v + 'T00:00:00Z').toISOString();
      if (tipo === 'numero') v = parseInt(v || '0', 10);
      ponRuta(datos, ruta, v);
    });
    return datos;
  }

  async function api(entidad, metodo, id, cuerpo){
    const res = await fetch('/api/v1/' + entidad + (id ? '/' + id : ''), {
      method: metodo, credentials: 'same-origin',
      headers: { 'Content-Type':'application/json' },
      body: cuerpo ? JSON.stringify(cuerpo) : undefined
    });
    if (!res.ok){
      const d = await res.json().catch(()=>({}));
      throw new Error(d.detail || d.message || ('Error ' + res.status));
    }
    return res.status === 204 ? null : res.json().catch(()=>null);
  }

  async function abrir(entidad, accion, id){
    const esquema = ESQUEMAS[entidad];
    if (!esquema) return;
    let item = {};
    if (accion !== 'nuevo'){
      try { item = await api(entidad, 'GET', id); }
      catch (e){ estado('No se pudo cargar: ' + e.message); return; }
    } else {
      try {
        const lista = await api(entidad, 'GET', 'admin');
        item = { order: Array.isArray(lista) ? lista.length : 0 };
      } catch (e){ item = { order: 0 }; }
    }
    actual = { entidad, id, item, esquema, accion };

    if (accion === 'borrar'){
      titulo.textContent = 'Eliminar ' + esquema.titulo.toLowerCase();
      forma.innerHTML = `<p>¿Eliminar <strong>${item.title || item.name || item.venue || ''}</strong>? No se puede deshacer.</p>`;
      btnGuardar.hidden = true; btnBorrar.hidden = false;
    } else {
      titulo.textContent = (accion === 'nuevo' ? 'Nuevo: ' : 'Editar: ') + esquema.titulo.toLowerCase();
      forma.innerHTML = esquema.campos.map((c)=> campoHtml(c, porRuta(item, c[0]))).join('');
      if (accion === 'nuevo') forma.querySelectorAll('input[type="checkbox"]').forEach((c)=> c.checked = true);
      btnGuardar.hidden = false; btnBorrar.hidden = accion === 'nuevo';
      const slug = document.getElementById('f-slug'), tit = document.getElementById('f-title');
      if (slug && tit && accion === 'nuevo') tit.addEventListener('input', ()=>{
        if (slug.value) return;
        slug.value = tit.value.toLowerCase().normalize('NFD').replaceAll(/[\u0300-\u036f]/g, '').replaceAll(/[^a-z0-9]+/g, '-').replaceAll(/^-+|-+$/g, '');
      });
    }
    modal.hidden = false;
  }

  function cerrar(){ modal.hidden = true; actual = null; }

  function estado(msg){
    const bar = parent.document ? document.getElementById('estado') : null;
    if (bar) bar.textContent = msg;
  }

  btnGuardar.addEventListener('click', async ()=>{
    if (!actual) return;
    btnGuardar.disabled = true;
    try {
      const datos = leer(actual.esquema);
      if (actual.accion === 'nuevo') await api(actual.entidad, 'POST', null, datos);
      else await api(actual.entidad, 'PUT', actual.id, { ...actual.item, ...datos });
      cerrar();
      document.getElementById('lienzo').contentWindow.location.reload();
    } catch (e){ estado('No se pudo guardar: ' + e.message); }
    btnGuardar.disabled = false;
  });

  btnBorrar.addEventListener('click', async ()=>{
    if (!actual || !actual.id) return;
    btnBorrar.disabled = true;
    try {
      await api(actual.entidad, 'DELETE', actual.id);
      cerrar();
      document.getElementById('lienzo').contentWindow.location.reload();
    } catch (e){ estado('No se pudo eliminar: ' + e.message); }
    btnBorrar.disabled = false;
  });

  modal.addEventListener('click', (e)=>{ if (e.target === modal) cerrar(); });
  document.getElementById('crudCancelar').addEventListener('click', cerrar);

  window.addEventListener('message', (e)=>{
    if (e.origin !== location.origin || !e.data) return;
    if (e.data.tipo === 'crud') abrir(e.data.entidad, e.data.accion, e.data.id);
  });
})();
