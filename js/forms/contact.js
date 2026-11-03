// forms/contact — validación inline + anti-robot (respuesta en data-answer).
(function () {
  const form = document.getElementById('contactForm');
  if (!form) return;
  const regexMail = /^([a-z0-9_-]+\.)*[a-z0-9_-]+@[a-z0-9_-]+(\.[a-z0-9_-]+)*\.[a-z]{2,6}$/;
  const set = (id, m) => { const el = document.getElementById(id); if (el) el.textContent = m; };
  const robotOk = () => document.getElementById('checkRobot').dataset.answer || '7';
  document.getElementById('nom')?.addEventListener('input', function () { set('helpNom', this.value.length >= 50 ? 'Máx. 50 caracteres.' : ''); });
  document.getElementById('mail')?.addEventListener('input', function () { set('helpMail', this.value && !regexMail.test(this.value) ? 'Email incorrecto.' : ''); });
  document.getElementById('message')?.addEventListener('input', function () { set('helpMessage', this.value.length >= 3000 ? 'Máx. 3000 caracteres.' : ''); });
  document.getElementById('checkRobot')?.addEventListener('input', function () { set('helpRobot', this.value && this.value != robotOk() ? 'Resultado incorrecto.' : ''); });
  form.addEventListener('submit', function (e) {
    e.preventDefault();
    if (document.getElementById('checkRobot').value != robotOk()) { set('helpRobot', 'Por favor responde la verificación.'); return; }
    const T = window.__SITE_TEXTS__ || {};
    document.getElementById('form-response').textContent = T['contact.success'] || '¡Mensaje enviado! Gracias por contactarnos.';
    this.reset();
  });
})();
