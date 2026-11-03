using MVCCoreApp.Models;

namespace MVCCoreApp.Services;

/// <summary>Registra acciones sensibles sin interrumpir la operación principal si falla.</summary>
public class AuditoriaService(
    MongoDbService mongo,
    IHttpContextAccessor http,
    ILogger<AuditoriaService> bitacora)
{
    public async Task RegistrarAsync(string accion, string detalle, string? usuario = null)
    {
        var contexto = http.HttpContext;
        var evento = new Auditoria
        {
            Usuario = usuario ?? contexto?.User?.Identity?.Name ?? "anónimo",
            Accion = accion,
            Detalle = detalle,
            Ip = contexto?.Connection?.RemoteIpAddress?.ToString(),
            CreatedBy = "auditoria",
            UpdatedBy = "auditoria",
        };

        try
        {
            await mongo.GetCollection<Auditoria>("auditLog").InsertOneAsync(evento);
        }
        catch (Exception error)
        {
            // La auditoría es best-effort: no debe tumbar la operación principal.
            bitacora.LogError(error, "No se pudo guardar el evento de auditoría {Accion}.", accion);
        }
    }
}
