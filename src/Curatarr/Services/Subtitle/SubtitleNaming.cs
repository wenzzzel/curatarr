namespace Curatarr.Services.Subtitle;

public static class SubtitleNaming
{
    private const string OriginalToken = ".original";
    private const string SrtExtension = ".srt";

    public static bool IsOriginalVariant(string suffix) =>
        suffix.EndsWith(OriginalToken + SrtExtension, StringComparison.OrdinalIgnoreCase);

    public static string ToOriginalVariant(string suffix)
    {
        if (IsOriginalVariant(suffix)) return suffix;
        if (!suffix.EndsWith(SrtExtension, StringComparison.OrdinalIgnoreCase)) return suffix;
        return string.Concat(suffix.AsSpan(0, suffix.Length - SrtExtension.Length), OriginalToken, SrtExtension);
    }

    public static string FromOriginalVariant(string suffix)
    {
        if (!IsOriginalVariant(suffix)) return suffix;
        var endLen = OriginalToken.Length + SrtExtension.Length;
        return string.Concat(suffix.AsSpan(0, suffix.Length - endLen), SrtExtension);
    }

    /// <summary>
    /// True if a destination subtitle with <paramref name="destSuffix"/> covers a source
    /// subtitle with <paramref name="sourceSuffix"/> — either an exact suffix match or the
    /// destination is the renamed `.original` variant of the source's suffix.
    /// </summary>
    public static bool DestinationSatisfiesSource(string destSuffix, string sourceSuffix) =>
        string.Equals(destSuffix, sourceSuffix, StringComparison.OrdinalIgnoreCase)
        || string.Equals(destSuffix, ToOriginalVariant(sourceSuffix), StringComparison.OrdinalIgnoreCase);
}
