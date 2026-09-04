// Configuración front desacoplado — cambiar API_URL según entorno
// Para GitHub Pages custom domain: deja prod URL
// Para local dev: usa http://localhost:5299 o https://localhost:7092
window.__CONFIG__ = {
  API_URL: "http://localhost:5299", // TODO: cambiar a https://tu-api.onrender.com en prod
  // Si despliegas back en otro servicio, cambia aquí y haz commit
};
