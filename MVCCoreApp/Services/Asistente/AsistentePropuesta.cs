namespace MVCCoreApp.Services.Asistente;

/// <summary>Propuesta legible que el asistente muestra antes de escribir o como resultado de una consulta.</summary>
public record AsistentePropuesta(string Titulo, string Detalle, string ArgumentosJson);
