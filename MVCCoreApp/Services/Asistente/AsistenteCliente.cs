using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MVCCoreApp.Services.Asistente;

/// <summary>Cliente HTTP del API de OpenCode Go (formato chat-completions de OpenAI).</summary>
public class AsistenteCliente(HttpClient http, IConfiguration configuracion, ILogger<AsistenteCliente> bitacora)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public bool Configurado
    {
        get
        {
            var clave = configuracion["Asistente:ApiKey"];
            return !string.IsNullOrWhiteSpace(clave);
        }
    }

    public async Task<JsonNode?> CompletarAsync(
        string instrucciones,
        List<(string Rol, string Texto)> historial,
        JsonArray herramientas,
        string sesion,
        CancellationToken cancelacion = default)
    {
        var clave = configuracion["Asistente:ApiKey"];
        if (string.IsNullOrWhiteSpace(clave))
        {
            return null;
        }

        var baseUrl = (configuracion["Asistente:BaseUrl"] ?? "https://opencode.ai/zen/go/v1").TrimEnd('/');
        var modelo = configuracion["Asistente:Modelo"] ?? "muse-spark-1.3-low";
        var maxTokens = int.TryParse(configuracion["Asistente:MaxTokens"], out var mt) ? mt : 16000;

        var mensajes = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = instrucciones } };
        foreach (var (rol, texto) in historial)
        {
            mensajes.Add(new JsonObject { ["role"] = rol, ["content"] = texto });
        }

        var cuerpo = new JsonObject
        {
            ["model"] = modelo,
            ["messages"] = mensajes,
            ["max_tokens"] = maxTokens,
            ["reasoning_effort"] = "low",
        };

        if (herramientas.Count > 0)
        {
            cuerpo["tools"] = herramientas.DeepClone();
            cuerpo["tool_choice"] = "auto";
        }

        try
        {
            using var peticion = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clave);
            peticion.Headers.Add("x-opencode-session", sesion);
            peticion.Headers.UserAgent.ParseAdd("ElRey-Asistente/1.0");
            peticion.Content = new StringContent(cuerpo.ToJsonString(Json), Encoding.UTF8, "application/json");

            using var respuesta = await http.SendAsync(peticion, cancelacion);
            var texto = await respuesta.Content.ReadAsStringAsync(cancelacion);
            if (!respuesta.IsSuccessStatusCode)
            {
                bitacora.LogWarning("Asistente respondió {Codigo}: {Cuerpo}", (int)respuesta.StatusCode, texto[..Math.Min(texto.Length, 500)]);
                return null;
            }

            return JsonNode.Parse(texto);
        }
        catch (Exception error)
        {
            bitacora.LogWarning(error, "No se pudo contactar al asistente.");
            return null;
        }
    }
}
