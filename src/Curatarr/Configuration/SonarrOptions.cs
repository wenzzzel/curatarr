namespace Curatarr.Configuration;

public class SonarrOptions
{
    public const string SectionName = "Sonarr";

    public string Url { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
}
