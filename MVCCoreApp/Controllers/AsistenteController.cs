using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Driver;
using MVCCoreApp.Dtos;
using MVCCoreApp.Models;
using MVCCoreApp.Services;
using MVCCoreApp.Services.Asistente;

namespace MVCCoreApp.Controllers;

/// <summary>Asistente del panel (solo admins por cookie). Chat con propuestas confirmadas.</summary>
[Authorize(AuthenticationSchemes = "Cookies,Basic", Roles = "Admin")]
[Route("admin/asistente")]
public class AsistenteController(
    MongoDbService mongo,
    AsistenteServicio asistente,
    AsistenteCliente cliente,
    LimiteAsistente limite) : Controller
{
    private string Usuario => User.Identity?.Name ?? "admin";

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewBag.Configurado = cliente.Configurado;
        return View("~/Views/Admin/Asistente.cshtml");
    }

    [HttpGet("hilos")]
    public async Task<IActionResult> Hilos()
    {
        var hilos = await mongo.GetCollection<HiloChat>("chatHilos")
            .Find(h => h.Usuario == Usuario)
            .SortByDescending(h => h.ModificadoEn)
            .Limit(20)
            .ToListAsync();
        return Ok(hilos.Select(h => new HiloChatDto(h.Id, h.Titulo, h.ModificadoEn)));
    }

    [HttpGet("hilos/{id}")]
    public async Task<IActionResult> Historial(string id)
    {
        var hilo = await mongo.GetCollection<HiloChat>("chatHilos")
            .Find(h => h.Id == id && h.Usuario == Usuario).FirstOrDefaultAsync();
        if (hilo is null)
        {
            return NotFound(new { mensaje = "Chat no encontrado." });
        }

        var mensajes = await mongo.GetCollection<MensajeChat>("chatMensajes")
            .Find(m => m.HiloId == id).SortBy(m => m.CreatedAt).ToListAsync();
        return Ok(mensajes.Select(ADto));
    }

    [HttpPost("mensaje")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("asistente")]
    public async Task<IActionResult> Mensaje([FromBody] ChatMensajeRequest peticion)
    {
        if (limite.Excedido(Usuario))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { mensaje = "Vas muy rápido. Espera un momento." });
        }

        if (peticion is null || string.IsNullOrWhiteSpace(peticion.Texto))
        {
            return BadRequest(new { mensaje = "El mensaje está vacío." });
        }

        var (respuesta, error) = await asistente.ConversarAsync(Usuario, peticion.HiloId, peticion.Texto);
        return respuesta is null
            ? StatusCode(StatusCodes.Status502BadGateway, new { mensaje = error })
            : Ok(respuesta);
    }

    [HttpPost("confirmar")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("asistente")]
    public async Task<IActionResult> Confirmar([FromBody] ChatConfirmarRequest peticion)
    {
        if (peticion is null || string.IsNullOrWhiteSpace(peticion.MensajeId))
        {
            return BadRequest(new { mensaje = "Falta la propuesta." });
        }

        var (respuesta, error) = await asistente.ConfirmarAsync(Usuario, peticion.MensajeId, peticion.Confirmar);
        return respuesta is null
            ? BadRequest(new { mensaje = error })
            : Ok(respuesta);
    }

    [HttpDelete("hilos/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarHilo(string id)
    {
        var hilo = await mongo.GetCollection<HiloChat>("chatHilos")
            .Find(h => h.Id == id && h.Usuario == Usuario).FirstOrDefaultAsync();
        if (hilo is null)
        {
            return NotFound(new { mensaje = "Chat no encontrado." });
        }

        // El historial del chat no es dato de negocio: se elimina físicamente a solicitud del admin.
        await mongo.GetCollection<MensajeChat>("chatMensajes").DeleteManyAsync(m => m.HiloId == id);
        await mongo.GetCollection<HiloChat>("chatHilos").DeleteOneAsync(h => h.Id == id);
        return NoContent();
    }

    private static MensajeChatDto ADto(MensajeChat mensaje) => new(
        mensaje.Id,
        mensaje.Rol.ToString().ToLowerInvariant(),
        mensaje.Contenido,
        mensaje.PropuestaEstado == EstadoPropuesta.Pendiente
            ? new PropuestaDto(mensaje.Id, "", mensaje.Contenido, "pendiente")
            : null,
        mensaje.CreatedAt);
}
