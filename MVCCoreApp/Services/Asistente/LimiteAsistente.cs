using Microsoft.Extensions.Caching.Memory;

namespace MVCCoreApp.Services.Asistente;

/// <summary>
/// Límites por segundo y por hora del asistente, por usuario. El límite por minuto
/// lo aplica el RateLimiter de ASP.NET. Se guarda en memoria (una sola instancia).
/// </summary>
public class LimiteAsistente(IMemoryCache cache)
{
    public bool Excedido(string usuario)
    {
        var ahora = DateTime.UtcNow;
        var clave = $"asistente:{usuario.Trim().ToLowerInvariant()}";
        var estado = cache.GetOrCreate(clave, entrada =>
        {
            entrada.Size = 1;
            entrada.SlidingExpiration = TimeSpan.FromHours(1);
            return new Estado();
        })!;

        lock (estado)
        {
            estado.Segundos.RemoveAll(s => s <= ahora.AddSeconds(-1));
            if (estado.Segundos.Count >= 3)
            {
                return true;
            }

            estado.Horas.RemoveAll(h => h <= ahora.AddHours(-1));
            if (estado.Horas.Count >= 300)
            {
                return true;
            }

            estado.Segundos.Add(ahora);
            estado.Horas.Add(ahora);
            return false;
        }
    }

    private sealed class Estado
    {
        public List<DateTime> Segundos { get; } = [];
        public List<DateTime> Horas { get; } = [];
    }
}
