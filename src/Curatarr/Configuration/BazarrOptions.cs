namespace Curatarr.Configuration;

public class BazarrOptions
{
    public const string SectionName = "Bazarr";

    public string Url { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
}
