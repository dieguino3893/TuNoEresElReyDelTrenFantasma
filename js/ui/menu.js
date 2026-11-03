// ui/menu — abre/cierra nav lateral.
(function () {
  const btn = document.getElementById('menuBtn');
  if (!btn) return;
  const ids = ['nav1', 'nav2', 'nav3', 'nav4', 'nav5', 'nav6'];
  const cls = ['v1', 'v2', 'v3', 'v4', 'v5', 'v6'];
  const open = () => { btn.classList.add('active'); ids.forEach((id, i) => document.getElementById(id)?.classList.add(cls[i])); };
  const close = () => { btn.classList.remove('active'); ids.forEach((id, i) => document.getElementById(id)?.classList.remove(cls[i])); };
  btn.addEventListener('click', () => btn.classList.contains('active') ? close() : open());
  ids.forEach((id) => document.getElementById(id)?.addEventListener('click', close));
  window.addEventListener('scroll', () => { if (btn.classList.contains('active')) close(); }, { passive: true });
  window.addEventListener('keydown', (e) => { if (e.key === 'Escape' && btn.classList.contains('active')) close(); });
})();
