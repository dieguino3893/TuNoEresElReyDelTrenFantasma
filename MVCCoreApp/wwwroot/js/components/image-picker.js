// Selector de imágenes (nube Media / subida). Sin dependencias.
let _targetInputId = null;
let _selectedUrl = null;
let _selectedName = null;
let _mediaCache = [];

function openImagePicker(inputId){
  _targetInputId = inputId;
  _selectedUrl = null;
  document.getElementById('btnSelect').disabled = true;
  document.getElementById('btnSelect').style.opacity = '.5';
  document.getElementById('imgPicker').classList.add('open');
  switchTab('cloud');
  loadMedia();
}
function closeImagePicker(){ document.getElementById('imgPicker').classList.remove('open'); }
function switchTab(t){
  document.getElementById('paneCloud').style.display = t==='cloud' ? 'block' : 'none';
  document.getElementById('paneUpload').style.display = t==='upload' ? 'block' : 'none';
  document.getElementById('tabCloud').classList.toggle('active', t==='cloud');
  document.getElementById('tabUpload').classList.toggle('active', t==='upload');
}
async function loadMedia(){
  const grid = document.getElementById('mediaGrid');
  const msg = document.getElementById('mediaMsg');
  const q = document.getElementById('searchMedia').value.toLowerCase();
  grid.innerHTML = '<div class="mono">Cargando...</div>';
  try{
    let data = [];
    // intenta Media (con nombre), fallback a Cloudinary files
    try{ const r1 = await fetch('/api/v1/media', { credentials:'include' }); if(r1.ok) data = await r1.json(); } catch(e){}
    if(!data.length){
      const r2 = await fetch('/api/v1/files?prefix=elrey&max=100', { credentials:'include' });
      if(r2.ok){
        const files = await r2.json();
        data = files.map(f=>({ name: f.publicId.split('/').pop(), url: f.url, createdAt: f.createdAt, publicId: f.publicId }));
      }
    }
    _mediaCache = data;
    const filtered = q ? data.filter(m => (m.name||'').toLowerCase().includes(q)) : data;
    if(!filtered.length){ grid.innerHTML = '<div class="mono">No hay imágenes. Sube una nueva con nombre.</div>'; return; }
    grid.innerHTML = filtered.map(m => `
      <div class="card-img" data-url="${m.url}" data-name="${m.name}" data-id="${m.id || ''}" data-publicid="${m.publicId || ''}" onclick="selectCard(this)">
        <img src="${m.url}" alt="${m.name}" loading="lazy"/>
        <div style="padding:8px">
          <div style="font-size:.82rem;font-weight:600;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">${m.name}</div>
          <div class="mono" style="font-size:.62rem">${m.createdAt ? new Date(m.createdAt).toLocaleDateString() : ''}</div>
        </div>
        <button class="card-del" title="Eliminar de la nube" aria-label="Eliminar ${m.name}">🗑</button>
      </div>
    `).join('');
    msg.textContent = filtered.length + ' imágenes disponibles';
  }catch(e){ grid.innerHTML = '<div style="color:#FB7185">Error: '+e.message+'</div>'; }
}
async function delImagen(btn){
  const card = btn.closest('.card-img');
  if(!card) return;
  const nombre = card.dataset.name || 'esta imagen';
  if(!confirm('¿Eliminar ' + nombre + ' de la nube? Las páginas que la usen quedarán sin foto.')) return;
  btn.disabled = true;
  try{
    // Media tiene id propio; el fallback de files solo trae publicId.
    const url = card.dataset.id ? '/api/v1/media/' + card.dataset.id : '/api/v1/files/' + encodeURIComponent(card.dataset.publicid);
    const res = await fetch(url, { method:'DELETE', credentials:'include' });
    if(!res.ok) throw new Error((await res.json().catch(()=>({}))).detail || 'Error ' + res.status);
    loadMedia();
  }catch(e){ alert('No se pudo eliminar: ' + e.message); btn.disabled = false; }
}
document.getElementById('mediaGrid').addEventListener('click', (e)=>{
  const del = e.target.closest('.card-del');
  if(del){ e.stopPropagation(); delImagen(del); }
}, true);
function selectCard(el){
  document.querySelectorAll('.card-img').forEach(c=>c.classList.remove('selected'));
  el.classList.add('selected');
  _selectedUrl = el.dataset.url;
  _selectedName = el.querySelector('div div')?.textContent || '';
  const btn = document.getElementById('btnSelect');
  btn.disabled = false; btn.style.opacity='1';
}
document.getElementById('btnSelect').addEventListener('click', ()=>{
  if(!_selectedUrl || !_targetInputId) return;
  const inp = document.getElementById(_targetInputId);
  if(inp){ inp.value = _selectedUrl; inp.dispatchEvent(new Event('input')); }
  closeImagePicker();
});
// upload con nombre
const upDrop = document.getElementById('upDrop');
const upFile = document.getElementById('upFile');
const upName = document.getElementById('upName');
const upMsg = document.getElementById('upMsg');
upDrop.addEventListener('click', ()=> upFile.click());
upDrop.addEventListener('dragover', e=>{ e.preventDefault(); upDrop.classList.add('drag'); });
upDrop.addEventListener('dragleave', ()=> upDrop.classList.remove('drag'));
upDrop.addEventListener('drop', e=>{ e.preventDefault(); upDrop.classList.remove('drag'); if(e.dataTransfer.files[0]){ upFile.files = e.dataTransfer.files; upName.value = upName.value || e.dataTransfer.files[0].name.replace(/\.[^/.]+$/,''); }});
upFile.addEventListener('change', ()=>{ if(upFile.files[0] && !upName.value) upName.value = upFile.files[0].name.replace(/\.[^/.]+$/,''); });
document.getElementById('btnUpload').addEventListener('click', async ()=>{
  const f = upFile.files[0];
  if(!f) return alert('Selecciona archivo');
  const name = upName.value.trim() || f.name.replace(/\.[^/.]+$/,'');
  upMsg.textContent = 'Subiendo...';
  const fd = new FormData(); fd.append('file', f); fd.append('name', name);
  try{
    const res = await fetch('/api/v1/media', { method:'POST', body: fd, credentials:'include' });
    const data = await res.json();
    if(!res.ok) throw new Error(data.detail||'Error');
    upMsg.textContent = 'Subido: ' + data.name;
    // seleccionar automáticamente
    _selectedUrl = data.url;
    const inp = document.getElementById(_targetInputId);
    if(inp) inp.value = data.url;
    loadMedia();
    switchTab('cloud');
  }catch(e){ upMsg.textContent = 'Error: '+e.message; }
});
document.getElementById('searchMedia').addEventListener('input', loadMedia);