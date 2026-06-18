namespace Curatarr.Services.Subtitle;

public static class SubtitleEquivalence
{
    private static readonly Dictionary<string, string> TwoToThreeLetter = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "eng",
        ["sv"] = "swe",
    };

    /// <summary>
    /// Returns the destination-side suffixes that, if present, mean the supplied downloaded
    /// suffix is excessive (an original-language equivalent is already there). Includes both
    /// the legacy bare form (".eng.srt") and the renamed form (".eng.original.srt") so the
    /// logic works during and after the rename migration.
    /// </summary>
    public static IReadOnlyList<string> GetOriginalEquivalents(string downloadedSuffix)
    {
        var firstDot = downloadedSuffix.IndexOf('.', 1);
        if (firstDot < 0) return [];

        var lang = downloadedSuffix[1..firstDot];
        if (!TwoToThreeLetter.TryGetValue(lang, out var threeLetter)) return [];

        var bare = "." + threeLetter + downloadedSuffix[firstDot..];
        return [bare, SubtitleNaming.ToOriginalVariant(bare)];
    }
}
