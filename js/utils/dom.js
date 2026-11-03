// utils/dom — helpers DOM compartidos (sin dependencias).
(function () {
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => [...r.querySelectorAll(s)];
  function esc(s) {
    return String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;')
      .replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }
  function setText(sel, text) {
    const el = $(sel);
    if (el && text) el.textContent = text;
  }
  function setHtml(sel, html) {
    const el = $(sel);
    if (el && html) el.innerHTML = html;
  }
  window.DomUtils = { $, $$, esc, setText, setHtml };
})();
