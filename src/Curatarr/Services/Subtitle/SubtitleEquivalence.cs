namespace Curatarr.Services.Subtitle;

public static class SubtitleEquivalence
{
    private static readonly Dictionary<string, string> TwoToThreeLetter = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "eng",
        ["sv"] = "swe",
    };

    public static string? GetOriginalEquivalent(string downloadedSuffix)
    {
        var firstDot = downloadedSuffix.IndexOf('.', 1);
        if (firstDot < 0) return null;

        var lang = downloadedSuffix[1..firstDot];
        if (!TwoToThreeLetter.TryGetValue(lang, out var threeLetter))
        {
            return null;
        }

        return "." + threeLetter + downloadedSuffix[firstDot..];
    }
}
