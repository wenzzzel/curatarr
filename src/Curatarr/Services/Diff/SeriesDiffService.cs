using Curatarr.Data;
using Curatarr.Services.Destination;
using Microsoft.EntityFrameworkCore;

namespace Curatarr.Services.Diff;

public record SeriesDiffRow(string Title, int? SonarrId, string? SourceFolder, string? DestinationFolder)
{
    public bool InSource => SourceFolder is not null;
    public bool InDestination => DestinationFolder is not null;
}

public class SeriesDiffService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    DestinationScanner scanner)
{
    public async Task<IReadOnlyList<SeriesDiffRow>> GetSeriesDiffAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var sourceSeries = await db.Series.OrderBy(s => s.Title).ToListAsync(ct);

        var destinationFolders = scanner.GetSeriesFolders()
            .ToDictionary(name => name, StringComparer.OrdinalIgnoreCase);

        var rows = new List<SeriesDiffRow>();
        var matchedDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var series in sourceSeries)
        {
            var sourceFolder = string.IsNullOrEmpty(series.Path) ? null : Path.GetFileName(series.Path);
            string? destinationMatch = null;

            if (sourceFolder is not null && destinationFolders.TryGetValue(sourceFolder, out var dest))
            {
                destinationMatch = dest;
                matchedDestinations.Add(dest);
            }

            rows.Add(new SeriesDiffRow(series.Title, series.SonarrId, sourceFolder, destinationMatch));
        }

        foreach (var folder in destinationFolders.Values)
        {
            if (matchedDestinations.Contains(folder)) continue;
            rows.Add(new SeriesDiffRow(folder, SonarrId: null, SourceFolder: null, DestinationFolder: folder));
        }

        return rows.OrderBy(r => r.Title).ToList();
    }
}
