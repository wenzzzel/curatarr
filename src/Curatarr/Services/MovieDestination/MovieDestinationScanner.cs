using Curatarr.Configuration;
using Microsoft.Extensions.Options;

namespace Curatarr.Services.MovieDestination;

public class MovieDestinationScanner(IOptions<MovieDestinationOptions> options)
{
    private readonly MovieDestinationOptions _options = options.Value;

    public IReadOnlyList<string> GetMovieFolders()
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
