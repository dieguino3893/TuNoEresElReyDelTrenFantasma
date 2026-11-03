// LEGACY — el cliente real vive en ./services/api.js (v4).
// Se mantiene para no romper referencias viejas; carga services/api.js si AppAPI falta.
(function () {
  if (window.AppAPI) return;
  var s = document.createElement('script');
  s.src = './js/services/api.js?v=4';
  document.head.appendChild(s);
})();
