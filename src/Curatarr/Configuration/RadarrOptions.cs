namespace Curatarr.Configuration;

public class RadarrOptions
{
    public const string SectionName = "Radarr";

    public string Url { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
}
