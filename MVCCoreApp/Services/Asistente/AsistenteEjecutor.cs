using System.Text.Json.Nodes;

namespace MVCCoreApp.Services.Asistente;

/// <summary>Valida las herramientas del asistente y ejecuta las escrituras ya confirmadas.</summary>
public class AsistenteEjecutor(AsistenteDatos datos)
{
    public async Task<(AsistentePropuesta?, string?)> ValidarAsync(string herramienta, JsonObject args)
    {
        try
        {
            return herramienta switch
            {
                "consultar" => await datos.ConsultarAsync(args),
                "reporte" => await datos.ReporteAsync(args),
                "calcular" => datos.Calcular(args),
                "crear" => await datos.CrearAsync(args),
                "actualizar" => await datos.ActualizarAsync(args),
                "eliminar" => await datos.EliminarAsync(args),
                _ => (null, $"No existe la herramienta {herramienta}."),
            };
        }
        catch (Exception)
        {
            return (null, "No pude entender los datos de esa acción. Revísalos e intenta de nuevo.");
        }
    }

    public async Task<(bool Ok, string Texto, string? Error)> EjecutarAsync(
        string herramienta,
        string argumentosJson,
        string usuario)
    {
        try
        {
            return await datos.EjecutarAsync(herramienta, argumentosJson, usuario);
        }
        catch (Exception)
        {
            return (false, "", "No se pudo ejecutar la acción.");
        }
    }
}
