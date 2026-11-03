using System.Text.Json.Nodes;

namespace MVCCoreApp.Services.Asistente;

/// <summary>Herramientas genéricas del asistente. El modelo compone casos de uso con estas.</summary>
public static class AsistenteHerramientas
{
    private static JsonObject Herramienta(string nombre, string descripcion, JsonObject propiedades, string[] requeridos) =>
        new()
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = nombre,
                ["description"] = descripcion,
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = propiedades,
                    ["required"] = new JsonArray(requeridos.Select(r => (JsonNode)r).ToArray()),
                    ["additionalProperties"] = false,
                },
            },
        };

    private static JsonObject Texto(string descripcion) =>
        new() { ["type"] = "string", ["description"] = descripcion };

    private static JsonObject Objeto(string descripcion) =>
        new() { ["type"] = "object", ["description"] = descripcion, ["additionalProperties"] = true };

    public static JsonArray Definiciones()
    {
        var entidad = new JsonObject
        {
            ["type"] = "string",
            ["enum"] = new JsonArray("shows", "members", "news", "tracks", "setlists", "rehearsals"),
            ["description"] = "Entidad del sitio.",
        };

        return new JsonArray(
            Herramienta("consultar",
                "Consulta cualquier entidad del sitio. Acepta filtros simples, rangos, orden, límite, agrupación y métricas. Úsala también para encontrar ids antes de actualizar o eliminar.",
                new JsonObject
                {
                    ["entidad"] = entidad.DeepClone(),
                    ["filtros"] = Objeto(
                        "Filtros. Claves: texto, id, tag, tipo, fecha, lugar, ciudad, nombre, titulo, rol. " +
                        "Un texto filtra por coincidencia. Un rango se manda como {desde, hasta}. " +
                        "Ejemplo: {\"texto\":\"León\",\"fecha\":{\"desde\":\"2026-11-01\",\"hasta\":\"2026-11-30\"}}. Fechas en aaaa-MM-dd."),
                    ["orden"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["description"] = "Ej. {\"campo\":\"fecha\",\"direccion\":\"desc\"}.",
                        ["properties"] = new JsonObject
                        {
                            ["campo"] = Texto("Campo por el que ordenar."),
                            ["direccion"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("asc", "desc") },
                        },
                    },
                    ["limite"] = new JsonObject { ["type"] = "integer", ["description"] = "Máximo de filas (1-100, default 20)." },
                    ["agruparPor"] = Texto("Campo para agrupar, ej. ciudad, tag, tipo."),
                    ["metricas"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["description"] = "Métricas por grupo. Ej. [{\"campo\":\"canciones\",\"operacion\":\"suma\"}].",
                        ["items"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["campo"] = Texto("Campo numérico."),
                                ["operacion"] = new JsonObject
                                {
                                    ["type"] = "string",
                                    ["enum"] = new JsonArray("suma", "promedio", "min", "max", "conteo"),
                                },
                            },
                            ["required"] = new JsonArray("campo", "operacion"),
                        },
                    },
                },
                ["entidad"]),
            Herramienta("reporte",
                "Resumen del sitio: conteos por entidad, próximos shows y última noticia.",
                new JsonObject
                {
                    ["periodo"] = Texto("aaaa-MM. Si se omite, resume todo sin filtro de mes."),
                },
                []),
            Herramienta("calcular",
                "Calculadora exacta: +, -, *, / y paréntesis. Úsala para cualquier cuenta.",
                new JsonObject { ["expresion"] = Texto("Ej. (2*200*9*30)/1000*3.5") },
                ["expresion"]),
            Herramienta("crear",
                "Propone crear un registro. No se guarda hasta que el usuario confirme. Consulta antes si necesitas ids o si faltan datos.",
                new JsonObject
                {
                    ["entidad"] = entidad.DeepClone(),
                    ["datos"] = Objeto(
                        "Datos del registro. shows: lugar, ciudad, fecha (aaaa-MM-dd), hora (HH:mm), googleMapsUrl, visible. " +
                        "members: nombre, rol, orden, activo, imageUrl, profileUrl. " +
                        "news: titulo, tag (Música | La banda), extracto, contenido, publicado. " +
                        "tracks: titulo, numero, tipo, visible. " +
                        "setlists: nombre, descripcion. " +
                        "rehearsals: lugar, fecha (aaaa-MM-dd), notas, setlistId, showId."),
                },
                ["entidad", "datos"]),
            Herramienta("actualizar",
                "Propone editar un registro existente. Manda solo los campos que cambian. Necesita el id; búscalo con consultar. No se guarda hasta confirmar.",
                new JsonObject
                {
                    ["entidad"] = entidad.DeepClone(),
                    ["id"] = Texto("Id del registro."),
                    ["datos"] = Objeto("Campos a cambiar. Mismas claves que crear."),
                },
                ["entidad", "id", "datos"]),
            Herramienta("eliminar",
                "Propone dar de baja lógica un registro (se oculta, no se borra). Necesita el id; búscalo con consultar. No se ejecuta hasta confirmar.",
                new JsonObject
                {
                    ["entidad"] = entidad.DeepClone(),
                    ["id"] = Texto("Id del registro."),
                },
                ["entidad", "id"]),
            Herramienta("confirmar_pendiente",
                "Ejecuta la propuesta pendiente del hilo cuando el usuario la confirma con palabras (ej. 'sí, regístralo'). Úsala solo si hay una propuesta pendiente.",
                new JsonObject(),
                []));
    }
}
