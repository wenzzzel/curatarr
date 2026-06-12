using Curatarr.Configuration;
using Microsoft.Extensions.Options;

namespace Curatarr.Services.Destination;

public class DestinationScanner(IOptions<SeriesDestinationOptions> options)
{
    private readonly SeriesDestinationOptions _options = options.Value;

    public IReadOnlyList<string> GetSeriesFolders()
    {
        if (string.IsNullOrWhiteSpace(_options.Root) || !Directory.Exists(_options.Root))
        {
            return [];
        }

        return Directory.EnumerateDirectories(_options.Root)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .ToList();
    }
}
