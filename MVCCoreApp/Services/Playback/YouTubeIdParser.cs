using System.Text.RegularExpressions;

namespace MVCCoreApp.Services.Playback;

/// <summary>Extrae el videoId de 11 chars de URLs de YouTube (watch / youtu.be / embed / shorts / music).</summary>
public static partial class YouTubeIdParser
{
    [GeneratedRegex(@"(?<=v=)[\w-]{11}|(?<=youtu\.be/)[\w-]{11}|(?<=embed/)[\w-]{11}|(?<=shorts/)[\w-]{11}", RegexOptions.Compiled)]
    private static partial Regex IdRegex();

    public static string? TryExtract(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        input = input.Trim();
        // Id directo
        if (input.Length == 11 && Regex.IsMatch(input, @"^[\w-]{11}$")) return input;
        var m = IdRegex().Match(input);
        return m.Success ? m.Value : null;
    }
}
