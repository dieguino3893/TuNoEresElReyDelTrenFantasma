// Cliente API desacoplado — vanilla, sin dependencias
(function(){
  const API = (window.__CONFIG__ && window.__CONFIG__.API_URL) || window.location.origin;

  async function getJson(path){
    const res = await fetch(`${API}${path}`, { headers: { 'Accept':'application/json' }});
    if(!res.ok) throw new Error(`${res.status} ${path}`);
    return res.json();
  }

  window.AppAPI = {
    getHome: () => getJson('/api/v1/home'),
    getSiteSettings: () => getJson('/api/v1/site-settings'),
    getMembers: () => getJson('/api/v1/members'),
    getShows: () => getJson('/api/v1/shows'),
    getNextShow: () => getJson('/api/v1/shows/next'),
    getNews: () => getJson('/api/v1/news'),
    getTracks: () => getJson('/api/v1/tracks'),
    getHealth: () => getJson('/health'),
  };

  console.log('[AppAPI] base', API);
})();
