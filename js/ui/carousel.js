// ui/carousel — scroll-snap + flechas + dots (re-inicializable tras hydrate).
window.initCarousel = function () {
  const track = document.getElementById('carouselTrack');
  if (!track) return;
  const dotsWrap = document.getElementById('carouselDots');
  const count = document.getElementById('carouselCount');
  if (!dotsWrap || !count) return;
  dotsWrap.innerHTML = '';
  const slides = [...track.children];
  const total = slides.length;
  if (total === 0) { count.textContent = '0 / 0'; return; }
  let index = 0;
  slides.forEach((_, i) => {
    const b = document.createElement('button');
    b.className = 'carousel-dot' + (i === 0 ? ' active' : '');
    b.setAttribute('aria-label', 'Ir a canción ' + (i + 1));
    b.addEventListener('click', () => goTo(i));
    dotsWrap.appendChild(b);
  });
  const dots = [...dotsWrap.children];
  const pBtn = document.getElementById('carouselPrev');
  const nBtn = document.getElementById('carouselNext');
  const vp = document.getElementById('carouselViewport');
  function update() {
    dots.forEach((d, i) => d.classList.toggle('active', i === index));
    count.textContent = (index + 1) + ' / ' + total;
    if (pBtn) { pBtn.disabled = index === 0; pBtn.style.opacity = index === 0 ? .45 : 1; }
    if (nBtn) { nBtn.disabled = index === total - 1; nBtn.style.opacity = index === total - 1 ? .45 : 1; }
  }
  function goTo(i) {
    index = Math.max(0, Math.min(total - 1, i));
    const w = vp.clientWidth;
    vp.scrollTo({ left: w * index, behavior: 'smooth' });
    update();
  }
  // shortcut: clona botones para soltar listeners previos
  if (pBtn) pBtn.replaceWith(pBtn.cloneNode(true));
  if (nBtn) nBtn.replaceWith(nBtn.cloneNode(true));
  document.getElementById('carouselPrev')?.addEventListener('click', () => goTo(index - 1));
  document.getElementById('carouselNext')?.addEventListener('click', () => goTo(index + 1));
  let ticking = false;
  vp.onscroll = null;
  vp.addEventListener('scroll', () => {
    if (ticking) return;
    ticking = true;
    requestAnimationFrame(() => {
      const newIndex = Math.round(vp.scrollLeft / vp.clientWidth);
      if (newIndex !== index) { index = newIndex; update(); }
      ticking = false;
    });
  }, { passive: true });
  update();
  window.addEventListener('resize', () => goTo(index));
  window._carouselGoTo = goTo;
};
if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', window.initCarousel);
else window.initCarousel();
