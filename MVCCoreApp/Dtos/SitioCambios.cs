namespace MVCCoreApp.Dtos;

/// <summary>Cambios que llegan desde el editor en vivo del sitio.</summary>
public class SitioCambios
{
    public Dictionary<string, string> Textos { get; set; } = new();
    public Dictionary<string, string> Ajustes { get; set; } = new();
    public Dictionary<string, bool> Secciones { get; set; } = new();
}
