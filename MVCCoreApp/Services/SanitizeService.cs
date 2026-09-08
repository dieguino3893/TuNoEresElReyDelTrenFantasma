using Ganss.Xss;

namespace MVCCoreApp.Services;

public static class SanitizeService
{
    private static readonly HtmlSanitizer _sanitizer = Create();

    private static HtmlSanitizer Create()
    {
        var s = new HtmlSanitizer();
        // permitir solo etiquetas seguras para banda/news
        s.AllowedTags.Clear();
        foreach(var t in new[]{"p","br","strong","b","em","i","u","ul","ol","li","a","h2","h3","h4","blockquote","span"})
            s.AllowedTags.Add(t);
        s.AllowedAttributes.Clear();
        s.AllowedAttributes.Add("href");
        s.AllowedAttributes.Add("title");
        s.AllowedAttributes.Add("target");
        s.AllowedAttributes.Add("rel");
        // solo http/https/mailto en href
        s.AllowedSchemes.Clear();
        s.AllowedSchemes.Add("http");
        s.AllowedSchemes.Add("https");
        s.AllowedSchemes.Add("mailto");
        s.AllowDataAttributes = false;
        return s;
    }

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        return _sanitizer.Sanitize(html);
    }

    public static string SanitizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        // para textos planos que se pintan con textContent: no hace falta sanear HTML, solo evitar que se guarde script como texto malicioso que luego se pinte con innerHTML
        // Si se usa textContent, <script> se muestra como texto, no se ejecuta. Devolvemos tal cual.
        return text;
    }
}
