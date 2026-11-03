using Microsoft.Extensions.Caching.Memory;

namespace MVCCoreApp.Services.Auth;

/// <summary>
/// Bloqueo temporal por usuario tras varios intentos fallidos. Se guarda en memoria
/// (una sola instancia) y se reinicia al acertar la contraseña.
/// </summary>
public class IntentosLogin(IMemoryCache cache)
{
    private const int MaximosFallos = 5;
    private static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

    public bool EstaBloqueado(string nombre)
    {
        if (!cache.TryGetValue(Clave(nombre), out Estado? estado) || estado is null)
        {
            return false;
        }

        lock (estado)
        {
            return estado.BloqueadoHasta > DateTime.UtcNow;
        }
    }

    public void RegistrarFallo(string nombre)
    {
        var clave = Clave(nombre);
        var estado = cache.GetOrCreate(clave, entrada =>
        {
            entrada.Size = 1;
            entrada.SlidingExpiration = DuracionBloqueo;
            return new Estado();
        })!;

        lock (estado)
        {
            estado.Fallos += 1;
            if (estado.Fallos >= MaximosFallos)
            {
                estado.BloqueadoHasta = DateTime.UtcNow.Add(DuracionBloqueo);
                estado.Fallos = 0;
            }
        }

        cache.Set(clave, estado, new MemoryCacheEntryOptions
        {
            Size = 1,
            SlidingExpiration = DuracionBloqueo,
        });
    }

    public void RegistrarExito(string nombre) => cache.Remove(Clave(nombre));

    private static string Clave(string nombre) => nombre.Trim().ToLowerInvariant();

    private sealed class Estado
    {
        public int Fallos { get; set; }
        public DateTime BloqueadoHasta { get; set; }
    }
}
