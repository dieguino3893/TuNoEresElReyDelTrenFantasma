using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MVCCoreApp.Controllers.Api.V1;

/// <summary>Diagnóstico temporal de acceso a YouTube desde la IP del servidor.</summary>
[ApiController]
[Route("api/v1/diag")]
[Authorize(Policy = "BasicOrCookie")]
public class DiagController : ControllerBase
{
    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        ConnectTimeout = TimeSpan.FromSeconds(10)
    })
    { Timeout = TimeSpan.FromSeconds(20) };

    [HttpGet("youtube/{id}")]
    public async Task<IActionResult> Youtube(string id)
    {
        var results = new List<object>();

        using (var req = new HttpRequestMessage(HttpMethod.Get,
                   $"https://www.youtube.com/watch?v={id}&bpctr=9999999999"))
        {
            req.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            req.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
            var sw = Stopwatch.StartNew();
            try
            {
                using var res = await Http.SendAsync(req);
                var body = await res.Content.ReadAsStringAsync();
                results.Add(new
                {
                    client = "watch",
                    status = (int)res.StatusCode,
                    finalUrl = res.RequestMessage?.RequestUri?.ToString(),
                    hasOgUrl = body.Contains("og:url", StringComparison.Ordinal),
                    bodyLength = body.Length,
                    ms = sw.ElapsedMilliseconds
                });
            }
            catch (Exception ex) { results.Add(new { client = "watch", error = ex.Message, ms = sw.ElapsedMilliseconds }); }
        }

        foreach (var spec in Specs(id))
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://www.youtube.com/youtubei/v1/player?prettyPrint=false");
                req.Content = new StringContent(spec.Body, Encoding.UTF8, "application/json");
                req.Headers.TryAddWithoutValidation("User-Agent", spec.UserAgent);
                req.Headers.TryAddWithoutValidation("X-Youtube-Client-Name", spec.ClientId);
                req.Headers.TryAddWithoutValidation("X-Youtube-Client-Version", spec.ClientVersion);
                req.Headers.TryAddWithoutValidation("Origin", "https://www.youtube.com");

                using var res = await Http.SendAsync(req);
                var text = await res.Content.ReadAsStringAsync();
                string? status = null, reason = null;
                var streaming = false;
                var formats = 0;
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.TryGetProperty("playabilityStatus", out var ps))
                    {
                        status = ps.TryGetProperty("status", out var s) ? s.GetString() : null;
                        reason = ps.TryGetProperty("reason", out var r) ? r.GetString() : null;
                    }
                    if (doc.RootElement.TryGetProperty("streamingData", out var sd) &&
                        sd.TryGetProperty("adaptiveFormats", out var af))
                    {
                        streaming = true;
                        formats = af.GetArrayLength();
                    }
                }
                catch { /* respuesta no JSON */ }
                results.Add(new
                {
                    client = spec.Name,
                    status = (int)res.StatusCode,
                    playability = status,
                    reason,
                    streaming,
                    formats,
                    bytes = text.Length,
                    ms = sw.ElapsedMilliseconds
                });
            }
            catch (Exception ex) { results.Add(new { client = spec.Name, error = ex.Message, ms = sw.ElapsedMilliseconds }); }
        }

        return Ok(results);
    }

    private static IEnumerable<(string Name, string ClientId, string ClientVersion, string UserAgent, string Body)> Specs(string id)
    {
        const string androidUa = "com.google.android.youtube/20.10.38 (Linux; U; Android 11) gzip";
        const string iosUa = "com.google.ios.youtube/20.10.4 (iPhone16,2; U; CPU iOS 18_3_1 like Mac OS X;)";
        const string tvUa = "Mozilla/5.0 (ChromiumStylePlatform) Cobalt/Version";

        yield return ("android", "3", "20.10.38", androidUa, Body("ANDROID", "20.10.38", id, new()
        {
            ["androidSdkVersion"] = 30,
            ["osName"] = "Android",
            ["osVersion"] = "11"
        }));

        yield return ("ios", "5", "20.10.4", iosUa, Body("IOS", "20.10.4", id, new()
        {
            ["deviceMake"] = "Apple",
            ["deviceModel"] = "iPhone16,2",
            ["osName"] = "iPhone",
            ["osVersion"] = "18.3.1.22D72"
        }));

        yield return ("tv_embedded", "85", "2.0", tvUa, Body("TVHTML5_SIMPLY_EMBEDDED_PLAYER", "2.0", id, new()
        {
            ["platform"] = "TV"
        }));

        yield return ("visionos", "101", "1.02", "Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15",
            Body("VISIONOS", "1.02", id, new()
            {
                ["deviceMake"] = "Apple",
                ["deviceModel"] = "RealityDevice17,1",
                ["osName"] = "visionOS",
                ["osVersion"] = "26.5.23O471"
            }));

        yield return ("web", "1", "2.20241126.01.00", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            Body("WEB", "2.20241126.01.00", id, new()));
    }

    private static string Body(string clientName, string clientVersion, string id, Dictionary<string, object> extra)
    {
        var client = new Dictionary<string, object>
        {
            ["clientName"] = clientName,
            ["clientVersion"] = clientVersion,
            ["hl"] = "en",
            ["gl"] = "US"
        };
        foreach (var kv in extra) client[kv.Key] = kv.Value;

        return JsonSerializer.Serialize(new
        {
            videoId = id,
            contentCheckOk = true,
            racyCheckOk = true,
            context = new { client }
        });
    }
}
