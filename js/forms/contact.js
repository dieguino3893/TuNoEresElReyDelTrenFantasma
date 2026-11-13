// forms/contact — validación inline del formulario.
(function () {
  const form = document.getElementById('contactForm');
  if (!form) return;
  const regexMail = /^([a-z0-9_-]+\.)*[a-z0-9_-]+@[a-z0-9_-]+(\.[a-z0-9_-]+)*\.[a-z]{2,6}$/;
  const set = (id, m) => { const el = document.getElementById(id); if (el) el.textContent = m; };
  document.getElementById('nom')?.addEventListener('input', function () { set('helpNom', this.value.length >= 50 ? 'Máx. 50 caracteres.' : ''); });
  document.getElementById('mail')?.addEventListener('input', function () { set('helpMail', this.value && !regexMail.test(this.value) ? 'Email incorrecto.' : ''); });
  document.getElementById('message')?.addEventListener('input', function () { set('helpMessage', this.value.length >= 3000 ? 'Máx. 3000 caracteres.' : ''); });
  form.addEventListener('submit', function (e) {
    e.preventDefault();
    const T = window.__SITE_TEXTS__ || {};
    document.getElementById('form-response').textContent = T['contact.success'] || '¡Mensaje enviado! Gracias por contactarnos.';
    this.reset();
  });
})();
