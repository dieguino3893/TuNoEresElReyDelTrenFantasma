using System.Globalization;
using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Driver;
using MVCCoreApp.Models;
using MVCCoreApp.Models.Base;

namespace MVCCoreApp.Services.Asistente;

/// <summary>
/// Capa de datos del asistente: consultas, reporte y CRUD sobre las entidades del sitio.
/// Toda escritura pasa por validación y confirmación. Solo admins (ver controlador).
/// </summary>
public class AsistenteDatos(MongoDbService mongo)
{
    private static readonly CultureInfo Mx = new("es-MX");

    private static readonly string[] Entidades =
        ["shows", "members", "news", "tracks", "setlists", "rehearsals"];

    public async Task<(AsistentePropuesta?, string?)> ConsultarAsync(JsonObject args)
    {
        var entidad = Entidad(args);
        if (entidad is null || !Entidades.Contains(entidad))
        {
            return (null, $"No puedo consultar \"{entidad}\". Entidades: {string.Join(", ", Entidades)}.");
        }

        var filtros = args["filtros"]?.AsObject() ?? new JsonObject();
        var filas = entidad switch
        {
            "shows" => await ConsultarShows(filtros),
            "members" => await ConsultarMiembros(filtros),
            "news" => await ConsultarNoticias(filtros),
            "tracks" => await ConsultarTracks(filtros),
            "setlists" => await ConsultarSetlists(filtros),
            _ => await ConsultarEnsayos(filtros),
        };

        filas = Ordenar(filas, args["orden"]?.AsObject());
        var (agrupadas, error) = Agrupar(filas, args);
        if (error is not null)
        {
            return (null, error);
        }

        var limite = Limite(args);
        var total = agrupadas!.Count;
        return (new AsistentePropuesta(
            "Consulta",
            Formatear(agrupadas.Take(limite).ToList(), total),
            "{}"), null);
    }

    public async Task<(AsistentePropuesta?, string?)> ReporteAsync(JsonObject args)
    {
        var periodo = args["periodo"]?.GetValue<string>()?.Trim();
        DateTime? desde = null;
        DateTime? hasta = null;
        if (!string.IsNullOrWhiteSpace(periodo) &&
            DateTime.TryParseExact(periodo + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var inicio))
        {
            desde = inicio;
            hasta = inicio.AddMonths(1).AddTicks(-1);
        }

        var shows = await Todos<Show>("shows");
        var enPeriodo = shows.Where(s => desde is null || (s.Date >= desde && s.Date <= hasta)).ToList();
        var proximos = shows.Where(s => s.IsVisible && s.Date >= DateTime.UtcNow)
            .OrderBy(s => s.Date).Take(3).ToList();
        var miembros = await Todos<Member>("members");
        var noticias = await Todos<News>("news");
        var tracks = await Todos<Track>("tracks");
        var setlists = await Todos<Setlist>("setlists");
        var ensayos = await Todos<Rehearsal>("rehearsals");
        var ensayosPeriodo = ensayos.Where(e => desde is null || (e.Date >= desde && e.Date <= hasta)).ToList();
        var ultimaNoticia = noticias.Where(n => n.IsPublished).OrderByDescending(n => n.PublishedAt).FirstOrDefault();

        var detalle = (desde is null ? "Resumen del sitio" : $"Resumen de {periodo}") + "\n" +
            $"Shows: {enPeriodo.Count} ({proximos.Count} próximos visibles)\n" +
            $"Miembros activos: {miembros.Count(m => m.IsActive)}\n" +
            $"Noticias publicadas: {noticias.Count(n => n.IsPublished)}\n" +
            $"Tracks visibles: {tracks.Count(t => t.IsVisible)}\n" +
            $"Setlists: {setlists.Count}\n" +
            $"Ensayos{(desde is null ? "" : " del mes")}: {ensayosPeriodo.Count}\n" +
            (proximos.Count > 0
                ? "Próximos:\n" + string.Join("\n", proximos.Select(s => $"- {s.Date:dd/MM/yyyy} {s.Time} · {s.Venue} ({s.CityState})"))
                : "Sin shows próximos.") + "\n" +
            (ultimaNoticia is null ? "Sin noticias." : $"Última noticia: {ultimaNoticia.Title} ({ultimaNoticia.PublishedAt:dd/MM/yyyy}).");

        return (new AsistentePropuesta("Reporte", detalle, "{}"), null);
    }

    public (AsistentePropuesta?, string?) Calcular(JsonObject args)
    {
        var expresion = args["expresion"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(expresion))
        {
            return (null, "Dime la operación que quieres calcular.");
        }

        try
        {
            var valor = new Calculadora(expresion).Evaluar();
            return (new AsistentePropuesta(
                "Cálculo",
                $"{expresion} = {valor.ToString("0.##", CultureInfo.InvariantCulture)}",
                "{}"), null);
        }
        catch (Exception)
        {
            return (null, "No pude calcular esa expresión. Usa números y + - * / paréntesis.");
        }
    }

    public async Task<(AsistentePropuesta?, string?)> CrearAsync(JsonObject args)
    {
        var entidad = Entidad(args);
        if (entidad is null || !Entidades.Contains(entidad))
        {
            return (null, $"No puedo crear \"{entidad}\".");
        }

        var datos = Datos(args);
        return entidad switch
        {
            "shows" => NormalizarShow(datos),
            "members" => NormalizarMiembro(datos),
            "news" => NormalizarNoticia(datos),
            "tracks" => await NormalizarTrack(datos),
            "setlists" => NormalizarSetlist(datos),
            _ => await NormalizarEnsayo(datos),
        };
    }

    public async Task<(AsistentePropuesta?, string?)> ActualizarAsync(JsonObject args)
    {
        var entidad = Entidad(args);
        var id = Id(args);
        if (entidad is null || !Entidades.Contains(entidad))
        {
            return (null, $"No puedo actualizar \"{entidad}\".");
        }

        if (id is null)
        {
            return (null, "Necesito el id del registro. Primero búscalo con consultar.");
        }

        var datos = Datos(args);
        return entidad switch
        {
            "shows" => await NormalizarExistente("shows", id, d => Task.FromResult(NormalizarShow(d, true)), datos),
            "members" => await NormalizarExistente("members", id, d => Task.FromResult(NormalizarMiembro(d, true)), datos),
            "news" => await NormalizarExistente("news", id, d => Task.FromResult(NormalizarNoticia(d, true)), datos),
            "tracks" => await NormalizarExistente("tracks", id, d => NormalizarTrackPatch(d, esParche: true), datos),
            "setlists" => await NormalizarExistente("setlists", id, d => Task.FromResult(NormalizarSetlist(d, true)), datos),
            _ => await NormalizarExistente("rehearsals", id, d => NormalizarEnsayo(d, true), datos),
        };
    }

    public async Task<(AsistentePropuesta?, string?)> EliminarAsync(JsonObject args)
    {
        var entidad = Entidad(args);
        var id = Id(args);
        if (entidad is null || !Entidades.Contains(entidad) || id is null)
        {
            return (null, "Dime la entidad y el id del registro. Primero búscalo con consultar.");
        }

        var descripcion = entidad switch
        {
            "shows" => await Describir<Show>("shows", id, s => $"{s.Venue} · {s.Date:dd/MM/yyyy}"),
            "members" => await Describir<Member>("members", id, m => $"{m.Name} ({m.Role})"),
            "news" => await Describir<News>("news", id, n => n.Title),
            "tracks" => await Describir<Track>("tracks", id, t => t.Title),
            "setlists" => await Describir<Setlist>("setlists", id, s => $"{s.Name} ({s.Items.Count} canciones)"),
            _ => await Describir<Rehearsal>("rehearsals", id, r => $"{r.Place} · {r.Date:dd/MM/yyyy}"),
        };

        if (descripcion is null)
        {
            return (null, "No encontré ese registro.");
        }

        return (new AsistentePropuesta(
            "Eliminar registro",
            $"Se dará de baja: {descripcion}.",
            new JsonObject { ["entidad"] = entidad, ["id"] = id }.ToJsonString()), null);
    }

    public async Task<(bool Ok, string Texto, string? Error)> EjecutarAsync(string herramienta, string argumentosJson, string usuario)
    {
        var args = JsonNode.Parse(argumentosJson)?.AsObject() ?? new JsonObject();
        var entidad = Entidad(args) ?? "";
        var datos = Datos(args);

        switch (herramienta)
        {
            case "crear":
                return await InsertarAsync(entidad, datos, usuario);
            case "actualizar":
                return await ActualizarDirectoAsync(entidad, datos, usuario);
            case "eliminar":
                return await EliminarDirectoAsync(entidad, Id(args), usuario);
            default:
                return (false, "", "Esa acción no se puede ejecutar.");
        }
    }

    // ---------- Consultas (colecciones chicas: filtra en memoria) ----------

    private async Task<List<T>> Todos<T>(string coleccion) =>
        await mongo.GetCollection<T>(coleccion).Find(FilterDefinition<T>.Empty).Limit(500).ToListAsync();

    private async Task<List<JsonObject>> ConsultarShows(JsonObject f)
    {
        var filas = await Todos<Show>("shows");
        var q = filas.Where(s => !s.IsDeleted);
        if (IdFiltro(f, "id") is { } id) q = q.Where(s => s.Id == id);
        if (Texto(f, "texto") is { } texto) q = q.Where(s =>
            Contiene(s.Venue, texto) || Contiene(s.CityState, texto));
        if (Texto(f, "lugar") is { } lugar) q = q.Where(s => Contiene(s.Venue, lugar));
        if (Texto(f, "ciudad") is { } ciudad) q = q.Where(s => Contiene(s.CityState, ciudad));
        var (desde, hasta) = RangoFecha(f, "fecha");
        if (desde is not null) q = q.Where(s => s.Date >= desde);
        if (hasta is not null) q = q.Where(s => s.Date <= hasta);

        return q.Select(s => new JsonObject
        {
            ["id"] = s.Id,
            ["fecha"] = s.Date.ToString("yyyy-MM-dd"),
            ["hora"] = s.Time,
            ["lugar"] = s.Venue,
            ["ciudad"] = s.CityState,
            ["visible"] = s.IsVisible ? "sí" : "no",
        }).ToList();
    }

    private async Task<List<JsonObject>> ConsultarMiembros(JsonObject f)
    {
        var filas = await Todos<Member>("members");
        var q = filas.Where(m => !m.IsDeleted);
        if (IdFiltro(f, "id") is { } id) q = q.Where(m => m.Id == id);
        if (Texto(f, "texto") is { } texto) q = q.Where(m =>
            Contiene(m.Name, texto) || Contiene(m.Role, texto));
        if (Texto(f, "nombre") is { } nombre) q = q.Where(m => Contiene(m.Name, nombre));
        if (Texto(f, "rol") is { } rol) q = q.Where(m => Contiene(m.Role, rol));

        return q.Select(m => new JsonObject
        {
            ["id"] = m.Id,
            ["nombre"] = m.Name,
            ["rol"] = m.Role,
            ["activo"] = m.IsActive ? "sí" : "no",
            ["orden"] = m.Order,
        }).ToList();
    }

    private async Task<List<JsonObject>> ConsultarNoticias(JsonObject f)
    {
        var filas = await Todos<News>("news");
        var q = filas.Where(n => !n.IsDeleted);
        if (IdFiltro(f, "id") is { } id) q = q.Where(n => n.Id == id);
        if (Texto(f, "texto") is { } texto) q = q.Where(n =>
            Contiene(n.Title, texto) || Contiene(n.Excerpt ?? "", texto));
        if (Texto(f, "titulo") is { } titulo) q = q.Where(n => Contiene(n.Title, titulo));
        if (Texto(f, "tag") is { } tag) q = q.Where(n => Contiene(n.Tag, tag));
        var (desde, hasta) = RangoFecha(f, "fecha");
        if (desde is not null) q = q.Where(n => n.PublishedAt >= desde);
        if (hasta is not null) q = q.Where(n => n.PublishedAt <= hasta);

        return q.Select(n => new JsonObject
        {
            ["id"] = n.Id,
            ["titulo"] = n.Title,
            ["tag"] = n.Tag,
            ["fecha"] = n.PublishedAt.ToString("yyyy-MM-dd"),
            ["publicado"] = n.IsPublished ? "sí" : "no",
        }).ToList();
    }

    private async Task<List<JsonObject>> ConsultarTracks(JsonObject f)
    {
        var filas = await Todos<Track>("tracks");
        var q = filas.Where(t => !t.IsDeleted);
        if (IdFiltro(f, "id") is { } id) q = q.Where(t => t.Id == id);
        if (Texto(f, "texto") is { } texto) q = q.Where(t => Contiene(t.Title, texto));
        if (Texto(f, "titulo") is { } titulo) q = q.Where(t => Contiene(t.Title, titulo));
        if (Texto(f, "tipo") is { } tipo) q = q.Where(t => Contiene(t.Type, tipo));

        return q.Select(t => new JsonObject
        {
            ["id"] = t.Id,
            ["titulo"] = t.Title,
            ["numero"] = t.TrackNumber,
            ["tipo"] = t.Type,
            ["visible"] = t.IsVisible ? "sí" : "no",
        }).ToList();
    }

    private async Task<List<JsonObject>> ConsultarSetlists(JsonObject f)
    {
        var filas = await Todos<Setlist>("setlists");
        var q = filas.Where(s => !s.IsDeleted);
        if (IdFiltro(f, "id") is { } id) q = q.Where(s => s.Id == id);
        if (Texto(f, "texto") is { } texto) q = q.Where(s => Contiene(s.Name, texto));
        if (Texto(f, "nombre") is { } nombre) q = q.Where(s => Contiene(s.Name, nombre));

        return q.Select(s => new JsonObject
        {
            ["id"] = s.Id,
            ["nombre"] = s.Name,
            ["canciones"] = s.Items.Count,
        }).ToList();
    }

    private async Task<List<JsonObject>> ConsultarEnsayos(JsonObject f)
    {
        var filas = await Todos<Rehearsal>("rehearsals");
        var q = filas.Where(r => !r.IsDeleted);
        if (IdFiltro(f, "id") is { } id) q = q.Where(r => r.Id == id);
        if (Texto(f, "texto") is { } texto) q = q.Where(r =>
            Contiene(r.Place, texto) || Contiene(r.Notes ?? "", texto));
        if (Texto(f, "lugar") is { } lugar) q = q.Where(r => Contiene(r.Place, lugar));
        var (desde, hasta) = RangoFecha(f, "fecha");
        if (desde is not null) q = q.Where(r => r.Date >= desde);
        if (hasta is not null) q = q.Where(r => r.Date <= hasta);

        return q.Select(r => new JsonObject
        {
            ["id"] = r.Id,
            ["fecha"] = r.Date.ToString("yyyy-MM-dd"),
            ["lugar"] = r.Place,
            ["notas"] = r.Notes ?? "",
        }).ToList();
    }

    // ---------- Normalizar (validar y proponer) ----------

    private static (AsistentePropuesta?, string?) NormalizarShow(JsonObject d, bool parche = false)
    {
        var lugar = Campo(d, "lugar") ?? Campo(d, "venue");
        if (!parche && string.IsNullOrWhiteSpace(lugar))
        {
            return (null, "Dime al menos el lugar del show.");
        }
        var fecha = FechaCampo(d, "fecha") ?? (parche ? null : FechaHoy());
        if (fecha is null)
        {
            return (null, "La fecha no es válida. Usa aaaa-MM-dd.");
        }
        var canon = new JsonObject
        {
            ["entidad"] = "shows",
            ["datos"] = new JsonObject
            {
                ["lugar"] = lugar?.Trim() ?? "",
                ["ciudad"] = Campo(d, "ciudad")?.Trim() ?? "",
                ["fecha"] = fecha.Value.ToString("yyyy-MM-dd"),
                ["hora"] = Campo(d, "hora")?.Trim() ?? "21:00",
                ["googleMapsUrl"] = Campo(d, "googleMapsUrl")?.Trim() ?? "",
                ["visible"] = Bool(d, "visible", true).ToString(),
            },
        };
        var detalle = $"{lugar} · {fecha.Value:dd/MM/yyyy}" +
            (string.IsNullOrWhiteSpace(Campo(d, "ciudad")) ? "" : $" ({Campo(d, "ciudad")?.Trim()})");
        return (new AsistentePropuesta(parche ? "Actualizar show" : "Crear show", detalle, canon.ToJsonString()), null);
    }

    private static (AsistentePropuesta?, string?) NormalizarMiembro(JsonObject d, bool parche = false)
    {
        var nombre = Campo(d, "nombre") ?? Campo(d, "name");
        if (!parche && string.IsNullOrWhiteSpace(nombre))
        {
            return (null, "Dime al menos el nombre del miembro.");
        }
        var canon = new JsonObject
        {
            ["entidad"] = "members",
            ["datos"] = new JsonObject
            {
                ["nombre"] = nombre?.Trim() ?? "",
                ["rol"] = Campo(d, "rol")?.Trim() ?? "",
                ["orden"] = (Numero(d, "orden") is { } o ? (int)o : 0).ToString(),
                ["activo"] = Bool(d, "activo", true).ToString(),
                ["imageUrl"] = Campo(d, "imageUrl")?.Trim() ?? "",
                ["profileUrl"] = Campo(d, "profileUrl")?.Trim() ?? "",
            },
        };
        return (new AsistentePropuesta(parche ? "Actualizar miembro" : "Crear miembro",
            $"{nombre} ({Campo(d, "rol")?.Trim()})", canon.ToJsonString()), null);
    }

    private static (AsistentePropuesta?, string?) NormalizarNoticia(JsonObject d, bool parche = false)
    {
        var titulo = Campo(d, "titulo") ?? Campo(d, "title");
        if (!parche && string.IsNullOrWhiteSpace(titulo))
        {
            return (null, "Dime al menos el título de la noticia.");
        }
        var canon = new JsonObject
        {
            ["entidad"] = "news",
            ["datos"] = new JsonObject
            {
                ["titulo"] = titulo?.Trim() ?? "",
                ["tag"] = Campo(d, "tag")?.Trim() ?? "Música",
                ["extracto"] = Campo(d, "extracto")?.Trim() ?? "",
                ["contenido"] = Campo(d, "contenido")?.Trim() ?? "",
                ["publicado"] = Bool(d, "publicado", true).ToString(),
            },
        };
        return (new AsistentePropuesta(parche ? "Actualizar noticia" : "Crear noticia",
            titulo?.Trim() ?? "", canon.ToJsonString()), null);
    }

    private async Task<(AsistentePropuesta?, string?)> NormalizarTrack(JsonObject d) =>
        await NormalizarTrackPatch(d, esParche: false);

    private async Task<(AsistentePropuesta?, string?)> NormalizarTrackPatch(JsonObject d, bool esParche)
    {
        var titulo = Campo(d, "titulo") ?? Campo(d, "title");
        if (!esParche && string.IsNullOrWhiteSpace(titulo))
        {
            return (null, "Dime al menos el título del track.");
        }
        var numero = (int)(Numero(d, "numero") ?? await SiguienteNumeroTrack());
        var canon = new JsonObject
        {
            ["entidad"] = "tracks",
            ["datos"] = new JsonObject
            {
                ["titulo"] = titulo?.Trim() ?? "",
                ["numero"] = numero.ToString(),
                ["tipo"] = Campo(d, "tipo")?.Trim() ?? "Single",
                ["visible"] = Bool(d, "visible", true).ToString(),
            },
        };
        return (new AsistentePropuesta(esParche ? "Actualizar track" : "Crear track",
            titulo?.Trim() ?? "", canon.ToJsonString()), null);
    }

    private static (AsistentePropuesta?, string?) NormalizarSetlist(JsonObject d, bool parche = false)
    {
        var nombre = Campo(d, "nombre") ?? Campo(d, "name");
        if (!parche && string.IsNullOrWhiteSpace(nombre))
        {
            return (null, "Dime al menos el nombre del setlist.");
        }
        var canon = new JsonObject
        {
            ["entidad"] = "setlists",
            ["datos"] = new JsonObject
            {
                ["nombre"] = nombre?.Trim() ?? "",
                ["descripcion"] = Campo(d, "descripcion")?.Trim() ?? "",
            },
        };
        return (new AsistentePropuesta(parche ? "Actualizar setlist" : "Crear setlist",
            nombre?.Trim() ?? "", canon.ToJsonString()), null);
    }

    private async Task<(AsistentePropuesta?, string?)> NormalizarEnsayo(JsonObject d, bool parche = false)
    {
        var lugar = Campo(d, "lugar") ?? Campo(d, "place");
        if (!parche && string.IsNullOrWhiteSpace(lugar))
        {
            return (null, "Dime al menos el lugar del ensayo.");
        }
        var fecha = FechaCampo(d, "fecha") ?? (parche ? null : FechaHoy());
        if (fecha is null)
        {
            return (null, "La fecha no es válida. Usa aaaa-MM-dd.");
        }
        var setlistId = VacioANulo(Campo(d, "setlistId"));
        if (setlistId is not null && !await Existe<Setlist>("setlists", setlistId))
        {
            return (null, "No encontré ese setlist. Búscalo con consultar.");
        }
        var showId = VacioANulo(Campo(d, "showId"));
        if (showId is not null && !await Existe<Show>("shows", showId))
        {
            return (null, "No encontré ese show. Búscalo con consultar.");
        }
        var canon = new JsonObject
        {
            ["entidad"] = "rehearsals",
            ["datos"] = new JsonObject
            {
                ["lugar"] = lugar?.Trim() ?? "",
                ["fecha"] = fecha.Value.ToString("yyyy-MM-dd"),
                ["notas"] = Campo(d, "notas")?.Trim() ?? "",
                ["setlistId"] = setlistId ?? "",
                ["showId"] = showId ?? "",
            },
        };
        return (new AsistentePropuesta(parche ? "Actualizar ensayo" : "Crear ensayo",
            $"{lugar} · {fecha.Value:dd/MM/yyyy}", canon.ToJsonString()), null);
    }

    private async Task<(AsistentePropuesta?, string?)> NormalizarExistente(
        string entidad, string id,
        Func<JsonObject, Task<(AsistentePropuesta?, string?)>> normalizar,
        JsonObject datos)
    {
        var existe = entidad switch
        {
            "shows" => await Existe<Show>("shows", id),
            "members" => await Existe<Member>("members", id),
            "news" => await Existe<News>("news", id),
            "tracks" => await Existe<Track>("tracks", id),
            "setlists" => await Existe<Setlist>("setlists", id),
            _ => await Existe<Rehearsal>("rehearsals", id),
        };
        if (!existe)
        {
            return (null, "No encontré ese registro.");
        }
        var (propuesta, error) = await normalizar(datos);
        if (propuesta is null)
        {
            return (null, error);
        }
        var args = JsonNode.Parse(propuesta.ArgumentosJson)!.AsObject();
        args["id"] = id;
        return (propuesta with { ArgumentosJson = args.ToJsonString() }, null);
    }

    // ---------- Ejecutar (escrituras ya confirmadas) ----------

    private async Task<(bool, string, string?)> InsertarAsync(string entidad, JsonObject datos, string usuario)
    {
        switch (entidad)
        {
            case "shows":
                var show = new Show
                {
                    Venue = Campo(datos, "lugar") ?? "",
                    CityState = Campo(datos, "ciudad") ?? "",
                    Date = FechaCampo(datos, "fecha") ?? FechaHoy(),
                    Time = Campo(datos, "hora") is { } h && h != "" ? h : "21:00",
                    GoogleMapsUrl = VacioANulo(Campo(datos, "googleMapsUrl")),
                    IsVisible = Bool(datos, "visible", true),
                    CreatedBy = usuario,
                    UpdatedBy = usuario,
                };
                await mongo.GetCollection<Show>("shows").InsertOneAsync(show);
                return (true, $"Show registrado: {show.Venue} · {show.Date:dd/MM/yyyy}.", null);
            case "members":
                var miembro = new Member
                {
                    Name = Campo(datos, "nombre") ?? "",
                    Role = Campo(datos, "rol") ?? "",
                    Order = (int)(Numero(datos, "orden") ?? 0),
                    IsActive = Bool(datos, "activo", true),
                    ImageUrl = Campo(datos, "imageUrl") ?? "",
                    ProfileUrl = Campo(datos, "profileUrl") ?? "",
                    CreatedBy = usuario,
                    UpdatedBy = usuario,
                };
                await mongo.GetCollection<Member>("members").InsertOneAsync(miembro);
                return (true, $"Miembro registrado: {miembro.Name}.", null);
            case "news":
                var titulo = Campo(datos, "titulo") ?? "";
                var noticia = new News
                {
                    Title = titulo,
                    Slug = SanitizeService.GenerarSlug(titulo),
                    Tag = Campo(datos, "tag") is { } t && t != "" ? t : "Música",
                    Excerpt = VacioANulo(Campo(datos, "extracto")),
                    ContentHtml = Campo(datos, "contenido") ?? "",
                    IsPublished = Bool(datos, "publicado", true),
                    PublishedAt = DateTime.UtcNow,
                    CreatedBy = usuario,
                    UpdatedBy = usuario,
                };
                await mongo.GetCollection<News>("news").InsertOneAsync(noticia);
                return (true, $"Noticia registrada: {noticia.Title}.", null);
            case "tracks":
                var track = new Track
                {
                    Title = Campo(datos, "titulo") ?? "",
                    TrackNumber = (int)(Numero(datos, "numero") ?? await SiguienteNumeroTrack()),
                    Type = Campo(datos, "tipo") is { } tt && tt != "" ? tt : "Single",
                    IsVisible = Bool(datos, "visible", true),
                    CreatedBy = usuario,
                    UpdatedBy = usuario,
                };
                await mongo.GetCollection<Track>("tracks").InsertOneAsync(track);
                return (true, $"Track registrado: {track.Title}.", null);
            case "setlists":
                var setlist = new Setlist
                {
                    Name = Campo(datos, "nombre") ?? "",
                    Description = VacioANulo(Campo(datos, "descripcion")),
                    CreatedBy = usuario,
                    UpdatedBy = usuario,
                };
                await mongo.GetCollection<Setlist>("setlists").InsertOneAsync(setlist);
                return (true, $"Setlist registrado: {setlist.Name}.", null);
            case "rehearsals":
                var setlistRef = VacioANulo(Campo(datos, "setlistId"));
                if (setlistRef is not null && !await Existe<Setlist>("setlists", setlistRef))
                {
                    return (false, "", "El setlist ya no existe. Pide la propuesta de nuevo.");
                }
                var showRef = VacioANulo(Campo(datos, "showId"));
                if (showRef is not null && !await Existe<Show>("shows", showRef))
                {
                    return (false, "", "El show ya no existe. Pide la propuesta de nuevo.");
                }
                var ensayo = new Rehearsal
                {
                    Place = Campo(datos, "lugar") ?? "",
                    Date = FechaCampo(datos, "fecha") ?? FechaHoy(),
                    Notes = VacioANulo(Campo(datos, "notas")),
                    SetlistId = VacioANulo(Campo(datos, "setlistId")),
                    ShowId = VacioANulo(Campo(datos, "showId")),
                    CreatedBy = usuario,
                    UpdatedBy = usuario,
                };
                await mongo.GetCollection<Rehearsal>("rehearsals").InsertOneAsync(ensayo);
                return (true, $"Ensayo registrado: {ensayo.Place} · {ensayo.Date:dd/MM/yyyy}.", null);
            default:
                return (false, "", "Esa entidad no se puede crear.");
        }
    }

    private async Task<(bool, string, string?)> ActualizarDirectoAsync(string entidad, JsonObject datos, string usuario)
    {
        var id = Id(datos);
        if (id is null)
        {
            return (false, "", "Falta el id del registro.");
        }

        switch (entidad)
        {
            case "shows":
                var show = await PorId<Show>("shows", id);
                if (show is null) return NoExiste();
                if (Campo(datos, "lugar") is { } v && v != "") show.Venue = v;
                if (Campo(datos, "ciudad") is { } c) show.CityState = c;
                if (FechaCampo(datos, "fecha") is { } f) show.Date = f;
                if (Campo(datos, "hora") is { } h && h != "") show.Time = h;
                if (Campo(datos, "googleMapsUrl") is { } g) show.GoogleMapsUrl = VacioANulo(g);
                if (Campo(datos, "visible") is { }) show.IsVisible = Bool(datos, "visible", show.IsVisible);
                show.UpdatedAt = DateTime.UtcNow; show.UpdatedBy = usuario;
                await Reemplazar("shows", show);
                return (true, $"Show actualizado: {show.Venue} · {show.Date:dd/MM/yyyy}.", null);
            case "members":
                var miembro = await PorId<Member>("members", id);
                if (miembro is null) return NoExiste();
                if (Campo(datos, "nombre") is { } n && n != "") miembro.Name = n;
                if (Campo(datos, "rol") is { } r) miembro.Role = r;
                if (Numero(datos, "orden") is { } o) miembro.Order = (int)o;
                if (Campo(datos, "activo") is { }) miembro.IsActive = Bool(datos, "activo", miembro.IsActive);
                if (Campo(datos, "imageUrl") is { } iu) miembro.ImageUrl = iu;
                if (Campo(datos, "profileUrl") is { } pu) miembro.ProfileUrl = pu;
                miembro.UpdatedAt = DateTime.UtcNow; miembro.UpdatedBy = usuario;
                await Reemplazar("members", miembro);
                return (true, $"Miembro actualizado: {miembro.Name}.", null);
            case "news":
                var noticia = await PorId<News>("news", id);
                if (noticia is null) return NoExiste();
                if (Campo(datos, "titulo") is { } t && t != "")
                {
                    noticia.Title = t;
                    noticia.Slug = SanitizeService.GenerarSlug(t);
                }
                if (Campo(datos, "tag") is { } tg && tg != "") noticia.Tag = tg;
                if (Campo(datos, "extracto") is { } e) noticia.Excerpt = VacioANulo(e);
                if (Campo(datos, "contenido") is { } ch) noticia.ContentHtml = ch;
                if (Campo(datos, "publicado") is { }) noticia.IsPublished = Bool(datos, "publicado", noticia.IsPublished);
                noticia.UpdatedAt = DateTime.UtcNow; noticia.UpdatedBy = usuario;
                await Reemplazar("news", noticia);
                return (true, $"Noticia actualizada: {noticia.Title}.", null);
            case "tracks":
                var track = await PorId<Track>("tracks", id);
                if (track is null) return NoExiste();
                if (Campo(datos, "titulo") is { } tt && tt != "") track.Title = tt;
                if (Numero(datos, "numero") is { } tn) track.TrackNumber = (int)tn;
                if (Campo(datos, "tipo") is { } tti && tti != "") track.Type = tti;
                if (Campo(datos, "visible") is { }) track.IsVisible = Bool(datos, "visible", track.IsVisible);
                track.UpdatedAt = DateTime.UtcNow; track.UpdatedBy = usuario;
                await Reemplazar("tracks", track);
                return (true, $"Track actualizado: {track.Title}.", null);
            case "setlists":
                var setlist = await PorId<Setlist>("setlists", id);
                if (setlist is null) return NoExiste();
                if (Campo(datos, "nombre") is { } sn && sn != "") setlist.Name = sn;
                if (Campo(datos, "descripcion") is { } sd) setlist.Description = VacioANulo(sd);
                setlist.UpdatedAt = DateTime.UtcNow; setlist.UpdatedBy = usuario;
                await Reemplazar("setlists", setlist);
                return (true, $"Setlist actualizado: {setlist.Name}.", null);
            case "rehearsals":
                var ensayo = await PorId<Rehearsal>("rehearsals", id);
                if (ensayo is null) return NoExiste();
                if (Campo(datos, "lugar") is { } el && el != "") ensayo.Place = el;
                if (FechaCampo(datos, "fecha") is { } ef) ensayo.Date = ef;
                if (Campo(datos, "notas") is { } en) ensayo.Notes = VacioANulo(en);
                if (Campo(datos, "setlistId") is { } es) ensayo.SetlistId = VacioANulo(es);
                if (Campo(datos, "showId") is { } esh) ensayo.ShowId = VacioANulo(esh);
                ensayo.UpdatedAt = DateTime.UtcNow; ensayo.UpdatedBy = usuario;
                await Reemplazar("rehearsals", ensayo);
                return (true, $"Ensayo actualizado: {ensayo.Place} · {ensayo.Date:dd/MM/yyyy}.", null);
            default:
                return (false, "", "Esa entidad no se puede actualizar.");
        }
    }

    private async Task<(bool, string, string?)> EliminarDirectoAsync(string entidad, string? id, string usuario)
    {
        if (id is null)
        {
            return (false, "", "Falta el id del registro.");
        }

        var ok = entidad switch
        {
            "shows" => await BajaLogica<Show>("shows", id, usuario),
            "members" => await BajaLogica<Member>("members", id, usuario),
            "news" => await BajaLogica<News>("news", id, usuario),
            "tracks" => await BajaLogica<Track>("tracks", id, usuario),
            "setlists" => await BajaLogica<Setlist>("setlists", id, usuario),
            "rehearsals" => await BajaLogica<Rehearsal>("rehearsals", id, usuario),
            _ => false,
        };
        return ok
            ? (true, "Registro dado de baja.", null)
            : (false, "", "El registro ya no existe.");
    }

    private static (bool, string, string?) NoExiste() => (false, "", "El registro ya no existe.");

    // ---------- Mongo helpers ----------

    private async Task<T?> PorId<T>(string coleccion, string id) where T : AuditableDocument =>
        await mongo.GetCollection<T>(coleccion).Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();

    private async Task<bool> Existe<T>(string coleccion, string id) where T : AuditableDocument =>
        await PorId<T>(coleccion, id) is not null;

    private async Task Reemplazar<T>(string coleccion, T doc) where T : AuditableDocument =>
        await mongo.GetCollection<T>(coleccion).ReplaceOneAsync(x => x.Id == doc.Id, doc);

    private async Task<bool> BajaLogica<T>(string coleccion, string id, string usuario) where T : AuditableDocument
    {
        var doc = await PorId<T>(coleccion, id);
        if (doc is null)
        {
            return false;
        }
        doc.IsDeleted = true;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.UpdatedBy = usuario;
        await Reemplazar(coleccion, doc);
        return true;
    }

    private async Task<string?> Describir<T>(string coleccion, string id, Func<T, string> texto) where T : AuditableDocument
    {
        var doc = await PorId<T>(coleccion, id);
        return doc is null ? null : texto(doc);
    }

    private async Task<int> SiguienteNumeroTrack()
    {
        var tracks = await Todos<Track>("tracks");
        return tracks.Count == 0 ? 1 : tracks.Max(t => t.TrackNumber) + 1;
    }


    private static string? VacioANulo(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static bool Contiene(string? valor, string texto) =>
        (valor ?? "").Contains(texto, StringComparison.OrdinalIgnoreCase);

    // ---------- JSON helpers ----------

    private static string? Entidad(JsonObject args) => Campo(args, "entidad")?.ToLowerInvariant();

    private static JsonObject Datos(JsonObject args) => args["datos"]?.AsObject() ?? new JsonObject();

    private static string? Id(JsonObject args) => IdFiltro(args, "id");

    private static string? IdFiltro(JsonObject? args, string clave)
    {
        var texto = Campo(args, clave);
        return ObjectId.TryParse(texto ?? "", out _) ? texto : null;
    }

    private static string? Campo(JsonObject? args, string clave)
    {
        var nodo = args?[clave];
        if (nodo is null) return null;
        if (nodo is JsonValue valor && valor.TryGetValue<string>(out var texto)) return texto;
        return nodo.ToString();
    }

    private static decimal? Numero(JsonObject? args, string clave)
    {
        var nodo = args?[clave];
        if (nodo is null) return null;
        if (nodo is JsonValue valor && valor.TryGetValue<decimal>(out var numero)) return numero;
        return decimal.TryParse(nodo.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parseado)
            ? parseado
            : null;
    }

    private static bool Bool(JsonObject? args, string clave, bool defecto)
    {
        var texto = Campo(args, clave)?.Trim().ToLowerInvariant();
        return texto switch
        {
            "true" or "sí" or "si" or "1" => true,
            "false" or "no" or "0" => false,
            _ => defecto,
        };
    }

    private static string? Texto(JsonObject? filtros, string clave)
    {
        var nodo = filtros?[clave];
        if (nodo is null) return null;
        if (nodo is JsonObject objeto)
        {
            return Campo(objeto, "contiene") ?? Campo(objeto, "igual");
        }
        return Campo(filtros, clave);
    }

    private static (DateTime? Desde, DateTime? Hasta) RangoFecha(JsonObject? filtros, string clave)
    {
        var nodo = filtros?[clave];
        if (nodo is null) return (null, null);
        if (nodo is JsonObject objeto)
        {
            DateTime? desde = Fecha(Campo(objeto, "desde"), out var d) ? d : null;
            DateTime? hasta = Fecha(Campo(objeto, "hasta"), out var h) ? h : null;
            if (hasta is not null) hasta = hasta.Value.Date.AddDays(1).AddTicks(-1);
            return (desde, hasta);
        }
        return Fecha(Campo(filtros, clave), out var fecha) ? (fecha, fecha.Date.AddDays(1).AddTicks(-1)) : (null, null);
    }

    private static bool Fecha(string? texto, out DateTime fecha)
    {
        if (DateTime.TryParseExact(texto?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha)
            && fecha.Year is >= 2000 and <= 2100)
        {
            return true;
        }
        if (DateTime.TryParseExact(texto?.Trim(), "dd/MM/yyyy", new CultureInfo("es-MX"), DateTimeStyles.None, out fecha)
            && fecha.Year is >= 2000 and <= 2100)
        {
            return true;
        }
        fecha = default;
        return false;
    }

    private static DateTime? FechaCampo(JsonObject? args, string clave)
    {
        var texto = Campo(args, clave);
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }
        return Fecha(texto, out var fecha) ? DateTime.SpecifyKind(fecha, DateTimeKind.Utc) : null;
    }

    private static DateTime FechaHoy() => DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

    private static int Limite(JsonObject args) =>
        Math.Clamp((int)(Numero(args, "limite") ?? 20), 1, 100);

    private static List<JsonObject> Ordenar(List<JsonObject> filas, JsonObject? orden)
    {
        var campo = Campo(orden, "campo");
        if (string.IsNullOrWhiteSpace(campo)) return filas;
        var desc = string.Equals(Campo(orden, "direccion"), "desc", StringComparison.OrdinalIgnoreCase);
        var comparador = Comparer<JsonNode?>.Create((a, b) =>
        {
            var sa = Valor(a);
            var sb = Valor(b);
            if (decimal.TryParse(sa, NumberStyles.Number, CultureInfo.InvariantCulture, out var da) &&
                decimal.TryParse(sb, NumberStyles.Number, CultureInfo.InvariantCulture, out var db))
            {
                return da.CompareTo(db);
            }
            return string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase);
        });
        return desc
            ? filas.OrderByDescending(f => f[campo], comparador).ToList()
            : filas.OrderBy(f => f[campo], comparador).ToList();
    }

    private static (List<JsonObject>? Filas, string? Error) Agrupar(List<JsonObject> filas, JsonObject args)
    {
        var campo = Campo(args, "agruparPor");
        var metricas = args["metricas"]?.AsArray();
        if (string.IsNullOrWhiteSpace(campo) && (metricas is null || metricas.Count == 0))
        {
            return (filas, null);
        }

        if (!string.IsNullOrWhiteSpace(campo) && filas.Count > 0 && !filas[0].ContainsKey(campo))
        {
            return (null, $"No puedo agrupar por \"{campo}\".");
        }

        var grupos = string.IsNullOrWhiteSpace(campo)
            ? filas.GroupBy(_ => "total")
            : filas.GroupBy(f => Valor(f[campo!]) ?? "—");
        var resultado = new List<JsonObject>();
        foreach (var grupo in grupos)
        {
            var fila = new JsonObject { ["conteo"] = grupo.Count() };
            if (!string.IsNullOrWhiteSpace(campo)) fila[campo] = grupo.Key;
            foreach (var metrica in metricas ?? new JsonArray())
            {
                var campoMetrica = Campo(metrica?.AsObject(), "campo");
                var operacion = Campo(metrica?.AsObject(), "operacion")?.ToLowerInvariant() ?? "suma";
                if (string.IsNullOrWhiteSpace(campoMetrica)) continue;
                var valores = grupo.Select(f => f[campoMetrica])
                    .Select(v => decimal.TryParse(Valor(v), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : (decimal?)null)
                    .Where(v => v is not null)
                    .Select(v => v!.Value)
                    .ToList();
                fila[$"{operacion}_{campoMetrica}"] = operacion switch
                {
                    "promedio" => valores.Count > 0 ? Math.Round(valores.Average(), 2) : 0,
                    "min" => valores.Count > 0 ? valores.Min() : 0,
                    "max" => valores.Count > 0 ? valores.Max() : 0,
                    "conteo" => valores.Count,
                    _ => valores.Sum(),
                };
            }
            resultado.Add(fila);
        }

        return (resultado, null);
    }

    private static string Formatear(List<JsonObject> filas, int total)
    {
        if (total == 0) return "Sin resultados.";
        var lineas = filas.Select(f =>
            "- " + string.Join(" | ", f.Select(kv => $"{kv.Key}: {Valor(kv.Value)}")));
        var cabeza = filas.Count < total ? $"Encontré {total} (muestro {filas.Count}):" : $"Encontré {total}:";
        return $"{cabeza}\n{string.Join("\n", lineas)}";
    }

    private static string Valor(JsonNode? nodo)
    {
        if (nodo is null) return "—";
        if (nodo is JsonValue valor && valor.TryGetValue<string>(out var texto)) return texto;
        return nodo.ToJsonString();
    }

    /// <summary>Calculadora aritmética sin eval: +, -, *, / y paréntesis.</summary>
    private sealed class Calculadora(string expresion)
    {
        private int _posicion;

        public decimal Evaluar()
        {
            var valor = Expresion();
            SaltarEspacios();
            if (_posicion < expresion.Length)
            {
                throw new FormatException();
            }
            return valor;
        }

        private decimal Expresion()
        {
            var valor = Termino();
            while (true)
            {
                SaltarEspacios();
                if (Coincide('+')) valor += Termino();
                else if (Coincide('-')) valor -= Termino();
                else return valor;
            }
        }

        private decimal Termino()
        {
            var valor = Factor();
            while (true)
            {
                SaltarEspacios();
                if (Coincide('*')) valor *= Factor();
                else if (Coincide('/'))
                {
                    var divisor = Factor();
                    if (divisor == 0) throw new DivideByZeroException();
                    valor /= divisor;
                }
                else return valor;
            }
        }

        private decimal Factor()
        {
            SaltarEspacios();
            if (Coincide('-')) return -Factor();
            if (Coincide('('))
            {
                var valor = Expresion();
                SaltarEspacios();
                if (!Coincide(')')) throw new FormatException();
                return valor;
            }

            var inicio = _posicion;
            while (_posicion < expresion.Length &&
                (char.IsDigit(expresion[_posicion]) || expresion[_posicion] is '.' or ','))
            {
                _posicion++;
            }

            var texto = expresion[inicio.._posicion].Replace(',', '.');
            if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero))
            {
                throw new FormatException();
            }
            return numero;
        }

        private bool Coincide(char caracter)
        {
            SaltarEspacios();
            if (_posicion < expresion.Length && expresion[_posicion] == caracter)
            {
                _posicion++;
                return true;
            }
            return false;
        }

        private void SaltarEspacios()
        {
            while (_posicion < expresion.Length && char.IsWhiteSpace(expresion[_posicion]))
            {
                _posicion++;
            }
        }
    }
}
