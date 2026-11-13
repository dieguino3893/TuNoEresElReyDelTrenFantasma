// forms/newsletter — suscripción vía back.
(function () {
  const form = document.getElementById('newsletterForm');
  if (!form) return;
  const input = document.getElementById('emailNews');
  const btn = document.getElementById('subBtn');
  const msg = document.getElementById('newsletter-msg');
  const API = (window.__CONFIG__ && window.__CONFIG__.API_URL) || window.location.origin;
  const T = () => window.__SITE_TEXTS__ || {};
  const setMsg = (t, type) => { msg.textContent = t; msg.className = type; };
  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    const email = input.value.trim();
    if (!email) return setMsg('Ingresa tu email.', 'error');
    btn.disabled = true;
    btn.innerHTML = '<span class="spin chico"></span>Enviando…';
    setMsg('', '');
    try {
      const res = await fetch(`${API}/api/v1/subscribe`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, source: 'web' }) });
      const data = await res.json();
      if (res.ok || res.status === 201) {
        setMsg(data.message || T()['nl.success'] || '¡Suscripción exitosa! Revisa tu correo.', 'ok');
        input.value = '';
        btn.innerHTML = '<i class="fa-solid fa-check" style="margin-right:6px;"></i> ¡Listo!';
        setTimeout(() => { btn.disabled = false; btn.innerHTML = '<i class="fa-solid fa-paper-plane" style="margin-right:6px;"></i> ' + (btn.dataset.label || 'Suscribirse'); }, 3000);
      } else throw new Error(data.detail || 'Error');
    } catch (err) {
      setMsg('Error de conexión. Inténtalo de nuevo.', 'error');
      btn.disabled = false;
      btn.innerHTML = '<i class="fa-solid fa-paper-plane" style="margin-right:6px;"></i> ' + (btn.dataset.label || 'Suscribirse');
    }
  });
})();
