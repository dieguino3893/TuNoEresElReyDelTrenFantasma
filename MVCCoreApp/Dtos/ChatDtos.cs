using System.ComponentModel.DataAnnotations;

namespace MVCCoreApp.Dtos;

public record HiloChatDto(string Id, string Titulo, DateTime ModificadoEn);

public record MensajeChatDto(
    string Id,
    string Rol,
    string Contenido,
    PropuestaDto? Propuesta,
    DateTime CreadoEn);

public record PropuestaDto(
    string MensajeId,
    string Titulo,
    string Detalle,
    string Estado);

public record ChatMensajeRequest(
    string? HiloId,
    [Required, StringLength(2000)] string Texto);

public record ChatConfirmarRequest(
    [Required] string MensajeId,
    [Required] bool Confirmar);
