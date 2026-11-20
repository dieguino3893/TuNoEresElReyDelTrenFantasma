namespace MVCCoreApp.Services.Playback;

/// <summary>Se lanza cuando ningún proveedor puede entregar audio para el id/query pedido.</summary>
public sealed class StreamNotAvailableException : Exception
{
    public string? Provider { get; }
    public IReadOnlyList<string> ProviderErrors { get; }
    /// <summary>VideoId de YouTube encontrado por búsqueda, para que el cliente pueda usar el embed.</summary>
    public string? VideoId { get; }

    public StreamNotAvailableException(string message, string? provider = null, IReadOnlyList<string>? errors = null, string? videoId = null)
        : base(message)
    {
        Provider = provider;
        ProviderErrors = errors ?? Array.Empty<string>();
        VideoId = videoId;
    }
}
