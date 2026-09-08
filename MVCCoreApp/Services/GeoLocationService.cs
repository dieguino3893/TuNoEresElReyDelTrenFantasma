using System.Text.Json;

namespace MVCCoreApp.Services;

public class ApproximateLocation
{
    public string? Country { get; set; }
    public string? Region { get; set; }
}

public interface IGeoLocationService
{
    Task<ApproximateLocation?> ResolveAsync(string? ip, CancellationToken ct = default);
}

public class GeoLocationService : IGeoLocationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GeoLocationService> _logger;

    public GeoLocationService(IHttpClientFactory httpClientFactory, ILogger<GeoLocationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ApproximateLocation?> ResolveAsync(string? ip, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ip) || ip == "::1" || ip.StartsWith("127.") || ip == "localhost")
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient("GeoIp");
            var json = await client.GetStringAsync($"http://ip-api.com/json/{ip}?fields=status,country,regionName", ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("status", out var status) && status.GetString() == "success")
            {
                var country = doc.RootElement.TryGetProperty("country", out var c) ? c.GetString() : null;
                var region = doc.RootElement.TryGetProperty("regionName", out var r) ? r.GetString() : null;
                return new ApproximateLocation { Country = country, Region = region };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GeoIP falló para IP {Ip}", ip);
        }

        return null;
    }
}
