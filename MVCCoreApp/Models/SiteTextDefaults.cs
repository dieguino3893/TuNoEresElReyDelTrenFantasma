namespace MVCCoreApp.Models;

/// <summary>Catálogo de textos editables del sitio: clave, grupo, etiqueta y valor inicial.</summary>
public static class SiteTextDefaults
{
    public record Def(string Key, string Group, string Label, string Value, bool Multiline = false);

    public static readonly IReadOnlyList<Def> All = new List<Def>
    {
        // Portada
        new("hero.scrollHint", "Portada", "Texto del botón para bajar", "Descubre"),
        // Próximo show
        new("show.kicker", "Próximo show", "Etiqueta superior", "Próximamente"),
        new("show.title", "Próximo show", "Título", "Próximo Show"),
        new("show.liveLabel", "Próximo show", "Etiqueta en vivo", "En vivo — No te lo pierdas"),
        new("show.cta", "Próximo show", "Enlace a todas las fechas", "Ver todas las fechas"),
        // Banda
        new("band.kicker", "Banda", "Etiqueta superior", "La banda"),
        // Shows
        new("shows.kicker", "Shows", "Etiqueta superior", "En vivo"),
        new("shows.title", "Shows", "Título", "Shows"),
        new("shows.note", "Shows", "Nota al pie", "* Próximamente las fechas se cargarán automáticamente desde el sistema de administración.", true),
        // Noticias
        new("blog.kicker", "Noticias", "Etiqueta superior", "Actualidad"),
        new("blog.title", "Noticias", "Título", "Noticias"),
        // Newsletter
        new("nl.kicker", "Newsletter", "Etiqueta superior", "Newsletter"),
        new("nl.title", "Newsletter", "Título", "Mantente al día"),
        new("nl.sub", "Newsletter", "Subtítulo", "No te pierdas los próximos shows ni novedades. Únete a la lista fantasma.", true),
        new("nl.placeholder", "Newsletter", "Texto del campo email", "tu@email.com"),
        new("nl.button", "Newsletter", "Texto del botón", "Suscribirse"),
        new("nl.success", "Newsletter", "Mensaje de éxito", "¡Suscripción exitosa! Revisa tu correo."),
        // Música
        new("music.kicker", "Música", "Etiqueta superior", "Escucha"),
        new("music.title", "Música", "Título", "< Nuestra Música >"),
        new("music.introSuffix", "Música", "Texto tras el número de singles", "singles — dale play sin salir de aquí. Desliza o usa las flechas.", true),
        new("music.profileName", "Música", "Nombre junto al perfil de Spotify", "Tú no eres el rey del tren fantasma"),
        new("music.alsoIn", "Música", "Etiqueta de plataformas", "Todas las canciones también en"),
        new("music.profileLabel", "Música", "Texto junto al perfil de Spotify", "Perfil oficial en Spotify"),
        new("music.platformSpotify", "Música", "Nombre de Spotify", "Spotify"),
        new("music.platformApple", "Música", "Nombre de Apple Music", "Apple"),
        new("music.platformYouTube", "Música", "Nombre de YouTube", "YouTube"),
        new("carousel.prev", "Música", "Etiqueta del botón anterior (accesibilidad)", "Anterior"),
        new("carousel.next", "Música", "Etiqueta del botón siguiente (accesibilidad)", "Siguiente"),
        new("site.empty", "General", "Mensaje cuando una sección no tiene contenido", "Aún muy fantasmal por aquí…"),
        // Navegación lateral
        new("nav.home", "Navegación", "Menú: Inicio", "Inicio"),
        new("nav.band", "Navegación", "Menú: Banda", "Banda"),
        new("nav.shows", "Navegación", "Menú: Shows", "Shows"),
        new("nav.music", "Navegación", "Menú: Música", "Música"),
        new("nav.blog", "Navegación", "Menú: Blog", "Blog"),
        new("nav.contact", "Navegación", "Menú: Contacto", "Contacto"),
        // Contacto
        new("contact.kicker", "Contacto", "Etiqueta superior", "Hablemos"),
        new("contact.title", "Contacto", "Título", "Contacto"),
        new("contact.hl", "Contacto", "Línea destacada", "— < Tú no eres el rey del tren fantasma > —", true),
        new("contact.city", "Contacto", "Ciudad", "León, Guanajuato, México"),
        new("contact.namePh", "Contacto", "Campo nombre", "Nombre"),
        new("contact.mailPh", "Contacto", "Campo email", "Email"),
        new("contact.telPh", "Contacto", "Campo teléfono", "Teléfono"),
        new("contact.msgPh", "Contacto", "Campo mensaje", "Cuéntanos algo — booking, prensa, colaboraciones…", true),
        new("contact.robotQ", "Contacto", "Pregunta anti-robot", "3 + 4 = ?"),
        new("contact.robotA", "Contacto", "Respuesta correcta", "7"),
        new("contact.submit", "Contacto", "Botón enviar", "Enviar mensaje"),
        new("contact.success", "Contacto", "Mensaje de éxito", "¡Mensaje enviado! Gracias por contactarnos.", true),
        // Pie de página
        new("foot.band", "Pie de página", "Nombre de la banda", "< Tú no eres el rey del tren fantasma >"),
        new("foot.city", "Pie de página", "Ciudad", "León, Guanajuato — México"),
        new("foot.legal", "Pie de página", "Línea legal", "Copyright 2026 · Aviso Legal"),
    };
}
