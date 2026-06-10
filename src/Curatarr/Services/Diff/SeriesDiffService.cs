using Curatarr.Data;
using Curatarr.Models;
using Curatarr.Services.Destination;
using Microsoft.EntityFrameworkCore;

namespace Curatarr.Services.Diff;

public record SeriesDiffRow(
    string Title,
    int? SonarrId,
    string? SourceFolder,
    string? DestinationFolder,
    int SourceEpisodes,
    int DestinationEpisodes)
{
    public bool InSource => SourceFolder is not null;
    public bool InDestination => DestinationFolder is not null;
    public int MissingEpisodes => Math.Max(0, SourceEpisodes - DestinationEpisodes);
}

public class SeriesDiffService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    DestinationScanner scanner)
{
    public async Task<IReadOnlyList<SeriesDiffRow>> GetSeriesDiffAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var seriesWithCounts = await db.Series
            .OrderBy(s => s.Title)
            .Select(s => new
            {
                s.Title,
                s.SonarrId,
                s.Path,
                SourceEpisodes = s.Episodes.Count(e => e.Files.Any(f => f.Side == FileSide.Source)),
                DestinationEpisodes = s.Episodes.Count(e => e.Files.Any(f => f.Side == FileSide.Destination)),
            })
            .ToListAsync(ct);

        var destinationFolders = scanner.GetSeriesFolders()
            .ToDictionary(name => name, StringComparer.OrdinalIgnoreCase);

        var rows = new List<SeriesDiffRow>();
        var matchedDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var series in seriesWithCounts)
        {
            var sourceFolder = string.IsNullOrEmpty(series.Path) ? null : Path.GetFileName(series.Path);
            string? destinationMatch = null;

            if (sourceFolder is not null && destinationFolders.TryGetValue(sourceFolder, out var dest))
            {
                destinationMatch = dest;
                matchedDestinations.Add(dest);
            }

            rows.Add(new SeriesDiffRow(
                series.Title,
                series.SonarrId,
                sourceFolder,
                destinationMatch,
                series.SourceEpisodes,
                series.DestinationEpisodes));
        }

        foreach (var folder in destinationFolders.Values)
        {
            if (matchedDestinations.Contains(folder)) continue;
            rows.Add(new SeriesDiffRow(
                folder,
                SonarrId: null,
                SourceFolder: null,
                DestinationFolder: folder,
                SourceEpisodes: 0,
                DestinationEpisodes: 0));
        }

        return rows.OrderBy(r => r.Title).ToList();
    }
}
