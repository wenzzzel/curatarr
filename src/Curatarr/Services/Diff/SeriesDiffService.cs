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
    int DestinationEpisodes,
    int OrphanedFiles)
{
    public bool InSource => SourceFolder is not null;
    public bool InDestination => DestinationFolder is not null;
    public int MissingEpisodes => Math.Max(0, SourceEpisodes - DestinationEpisodes);
}

public record SeriesDetail(
    string Title,
    int SonarrId,
    IReadOnlyList<EpisodeDiffRow> Episodes,
    IReadOnlyList<OrphanedFileRow> OrphanedFiles);

public record EpisodeDiffRow(
    int SonarrId,
    int SeasonNumber,
    int EpisodeNumber,
    string Title,
    string? SourceFile,
    string? DestinationFile)
{
    public bool HasSource => SourceFile is not null;
    public bool HasDestination => DestinationFile is not null;
}

public record OrphanedFileRow(string RelativePath, long SizeBytes);

public class SeriesDiffService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    DestinationScanner scanner)
{
    public async Task<IReadOnlyList<SeriesDiffRow>> GetSeriesDiffAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var seriesWithCounts = await db.Series
            .Where(s => s.Episodes.Any(e => e.Files.Any(f => f.Side == FileSide.Source)))
            .OrderBy(s => s.Title)
            .Select(s => new
            {
                s.Title,
                s.SonarrId,
                s.Path,
                SourceEpisodes = s.Episodes.Count(e => e.Files.Any(f => f.Side == FileSide.Source)),
                DestinationEpisodes = s.Episodes.Count(e => e.Files.Any(f => f.Side == FileSide.Destination)),
                OrphanedFiles = s.OrphanedDestinationFiles.Count(),
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
                series.DestinationEpisodes,
                series.OrphanedFiles));
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
                DestinationEpisodes: 0,
                OrphanedFiles: 0));
        }

        return [.. rows.OrderBy(r => r.Title)];
    }

    public async Task<SeriesDetail?> GetSeriesDetailAsync(int sonarrId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var series = await db.Series
            .Include(s => s.Episodes)
            .ThenInclude(e => e.Files)
            .Include(s => s.OrphanedDestinationFiles)
            .FirstOrDefaultAsync(s => s.SonarrId == sonarrId, ct);

        if (series is null) return null;

        var episodes = series.Episodes
            .OrderBy(e => e.SeasonNumber)
            .ThenBy(e => e.EpisodeNumber)
            .Select(e => new EpisodeDiffRow(
                e.SonarrId,
                e.SeasonNumber,
                e.EpisodeNumber,
                e.Title,
                e.SourceFile is { } sf ? Path.GetFileName(sf.RelativePath) : null,
                e.DestinationFile is { } df ? Path.GetFileName(df.RelativePath) : null))
            .ToList();

        var orphans = series.OrphanedDestinationFiles
            .OrderBy(o => o.RelativePath)
            .Select(o => new OrphanedFileRow(o.RelativePath, o.SizeBytes))
            .ToList();

        return new SeriesDetail(series.Title, series.SonarrId, episodes, orphans);
    }
}
