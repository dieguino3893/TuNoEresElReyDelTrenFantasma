// ui/scroll — scroll-down, flecha subir, reveal on scroll, email ofuscado, member cards.
(function () {
  const down = document.getElementById('scroll-down');
  if (down) {
    down.addEventListener('click', () => document.getElementById('next-show')?.scrollIntoView({ behavior: 'smooth' }));
    window.addEventListener('scroll', () => { down.style.opacity = window.scrollY >= 150 ? '0' : '1'; }, { passive: true });
  }
  const up = document.getElementById('up-arrow');
  if (up) {
    let lastY = 0;
    window.addEventListener('scroll', () => {
      const y = window.scrollY;
      up.style.display = (y > 600 && y < lastY) ? 'flex' : 'none';
      lastY = y;
    }, { passive: true });
    up.addEventListener('click', () => window.scrollTo({ top: 0, behavior: 'smooth' }));
    up.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') window.scrollTo({ top: 0, behavior: 'smooth' }); });
  }
  const els = document.querySelectorAll('.anim-y,.anim-x,.anim-fade');
  const check = () => { const h = window.innerHeight; els.forEach((el) => { if (el.getBoundingClientRect().top + 100 <= h) el.classList.add('showed'); }); };
  window.addEventListener('scroll', check, { passive: true });
  check();
  const mailBox = document.getElementById('insertMail');
  if (mailBox && !mailBox.querySelector('a')) {
    const name = 'tunoeresrelreydeltrenfantasma', domain = 'gmail.com';
    const a = document.createElement('a');
    a.href = 'mailto:' + name + '@' + domain;
    a.textContent = name + '@' + domain;
    mailBox.appendChild(a);
  }
  document.querySelectorAll('.member-card').forEach((c) => {
    if (c.dataset.bound) return;
    c.dataset.bound = '1';
    c.addEventListener('click', () => window.open(c.dataset.href || 'https://www.instagram.com/', '_blank'));
  });
})();
