namespace Curatarr.Services.Subtitle;

public enum SubtitleOrigin
{
    Downloaded = 0,
    Original = 1,
}

public static class SubtitleOriginClassifier
{
    public static SubtitleOrigin Classify(string suffix)
    {
        var firstDot = suffix.IndexOf('.', 1);
        if (firstDot < 0) return SubtitleOrigin.Downloaded;
        var languageToken = suffix[1..firstDot];
        return languageToken.Length == 3 ? SubtitleOrigin.Original : SubtitleOrigin.Downloaded;
    }
}
