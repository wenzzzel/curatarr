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

    /// <summary>
    /// Mirrors the copy job's priority: a source subtitle is already covered when the destination
    /// holds either the exact same suffix or its preferred three-letter equivalent.
    /// </summary>
    public static bool IsCoveredByDestination(string sourceSuffix, IReadOnlySet<string> destinationSuffixes)
    {
        if (destinationSuffixes.Contains(sourceSuffix)) return true;
        var original = GetOriginalEquivalent(sourceSuffix);
        return original is not null && destinationSuffixes.Contains(original);
    }

    public static HashSet<string> CreateSuffixSet(IEnumerable<string> suffixes) =>
        new(suffixes, StringComparer.OrdinalIgnoreCase);
}
