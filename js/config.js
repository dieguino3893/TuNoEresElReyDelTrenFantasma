// Configuración front desacoplado — cambiar API_URL según entorno
// Producción: back en https://reydeltrenfantasma.runasp.net (sin slash final)
// Para local dev: usa http://localhost:5299 o https://localhost:7092
window.__CONFIG__ = {
  API_URL: "https://reydeltrenfantasma.runasp.net",
  // Si despliegas back en otro servicio, cambia aquí y haz commit
};
