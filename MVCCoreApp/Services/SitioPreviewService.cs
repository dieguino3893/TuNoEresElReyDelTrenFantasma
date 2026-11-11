using System.Text.RegularExpressions;

namespace MVCCoreApp.Services;

/// <summary>Trae el HTML del sitio publicado (GitHub Pages) y lo deja listo para editarlo dentro del panel.</summary>
public class SitioPreviewService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public SitioPreviewService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task<string> ObtenerAsync(string adminBase)
    {
        var baseUrl = (_config["Frontend:PublicUrl"] ?? "https://elrey-del-trenfantasma.vellumdigitall.online").TrimEnd('/');
        var html = await _http.GetStringAsync(baseUrl + "/");

        // <base> resuelve solo css/, js/, img/ y favicons contra el sitio publicado.
        html = html.Replace("<head>", $"<head>\n<base href=\"{baseUrl}/\">", StringComparison.OrdinalIgnoreCase);

        // El lienzo habla con este back, no con el de producción (así funciona también en local).
        html = Regex.Replace(html, "<script src=\"\\./js/config\\.js[^\"]*\"[^>]*></script>",
            "<script>window.__CONFIG__={API_URL:location.origin}</script>");

        // Con <base> las rutas relativas al host apuntan al sitio publicado, así que el editor va con URL absoluta.
        return html.Replace("</body>", $"<script src=\"{adminBase}/js/admin-sitio-editor.js\"></script>\n</body>",
            StringComparison.OrdinalIgnoreCase);
    }
}
