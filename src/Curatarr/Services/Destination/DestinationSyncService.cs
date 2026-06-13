using Curatarr.Configuration;
using Curatarr.Data;
using Curatarr.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Curatarr.Services.Destination;

public record DestinationSyncResult(int SeriesScanned, int FilesMatched, int OrphanedFiles);

public class DestinationSyncService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    IOptions<SeriesDestinationOptions> options)
{
    private const string DestinationExtension = ".mp4";

    private readonly SeriesDestinationOptions _options = options.Value;

    public async Task<DestinationSyncResult> SyncAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Root) || !Directory.Exists(_options.Root))
        {
            return new DestinationSyncResult(0, 0, 0);
        }

        var now = DateTimeOffset.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var allSeries = await db.Series
            .Include(s => s.Episodes)
            .ThenInclude(e => e.Files)
            .Include(s => s.OrphanedDestinationFiles)
            .ToListAsync(ct);

        var destinationFolders = Directory.EnumerateDirectories(_options.Root)
            .Select(p => (Full: p, Leaf: Path.GetFileName(p)))
            .Where(x => !string.IsNullOrEmpty(x.Leaf))
            .ToDictionary(x => x.Leaf, x => x.Full, StringComparer.Ordinal);

        var seriesScanned = 0;
        var filesMatched = 0;
        var orphanedFiles = 0;

        foreach (var series in allSeries)
        {
            if (!series.Episodes.Any(e => e.Files.Any(f => f.Side == FileSide.Source)))
            {
                ClearDestinationFiles(series);
                ClearOrphans(series);
                continue;
            }

            var leafFolder = PathHelpers.GetLeafFolder(series.Path);
            if (leafFolder is null || !destinationFolders.TryGetValue(leafFolder, out var destFolder))
            {
                ClearDestinationFiles(series);
                ClearOrphans(series);
                continue;
            }

            seriesScanned++;
            var (matched, orphaned) = SyncSeriesDestinationFiles(series, destFolder, now);
            filesMatched += matched;
            orphanedFiles += orphaned;
        }

        await db.SaveChangesAsync(ct);
        return new DestinationSyncResult(seriesScanned, filesMatched, orphanedFiles);
    }

    private static (int Matched, int Orphaned) SyncSeriesDestinationFiles(Series series, string destFolder, DateTimeOffset now)
    {
        var episodesBySourceStem = series.Episodes
            .Select(e => new { Episode = e, Source = e.Files.FirstOrDefault(f => f.Side == FileSide.Source) })
            .Where(x => x.Source is not null)
            .ToLookup(
                x => Path.GetFileNameWithoutExtension(x.Source!.RelativePath),
                x => x.Episode,
                StringComparer.Ordinal);

        var matchedEpisodeIds = new HashSet<int>();
        var observedOrphans = new HashSet<string>(StringComparer.Ordinal);
        var matchCount = 0;
        var orphanCount = 0;

        foreach (var path in Directory.EnumerateFiles(destFolder, $"*{DestinationExtension}", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(destFolder, path);
            var size = new FileInfo(path).Length;
            var stem = Path.GetFileNameWithoutExtension(path);
            var episodes = episodesBySourceStem[stem].ToList();

            if (episodes.Count == 0)
            {
                UpsertOrphan(series, relativePath, size, now);
                observedOrphans.Add(relativePath);
                orphanCount++;
                continue;
            }

            foreach (var episode in episodes)
            {
                UpsertFile(episode, FileSide.Destination, relativePath, size, now);
                matchedEpisodeIds.Add(episode.SonarrId);
            }
            matchCount++;
        }

        foreach (var episode in series.Episodes)
        {
            if (matchedEpisodeIds.Contains(episode.SonarrId)) continue;
            RemoveFile(episode, FileSide.Destination);
        }

        var staleOrphans = series.OrphanedDestinationFiles
            .Where(o => !observedOrphans.Contains(o.RelativePath))
            .ToList();
        foreach (var stale in staleOrphans)
        {
            series.OrphanedDestinationFiles.Remove(stale);
        }

        return (matchCount, orphanCount);
    }

    private static void ClearDestinationFiles(Series series)
    {
        foreach (var episode in series.Episodes)
        {
            RemoveFile(episode, FileSide.Destination);
        }
    }

    private static void ClearOrphans(Series series)
    {
        series.OrphanedDestinationFiles.Clear();
    }

    private static void UpsertOrphan(Series series, string relativePath, long sizeBytes, DateTimeOffset now)
    {
        var existing = series.OrphanedDestinationFiles
            .FirstOrDefault(o => string.Equals(o.RelativePath, relativePath, StringComparison.Ordinal));
        if (existing is null)
        {
            series.OrphanedDestinationFiles.Add(new OrphanedDestinationFile
            {
                RelativePath = relativePath,
                SizeBytes = sizeBytes,
                ObservedAt = now,
            });
        }
        else
        {
            existing.SizeBytes = sizeBytes;
            existing.ObservedAt = now;
        }
    }

    private static void UpsertFile(Episode episode, FileSide side, string relativePath, long sizeBytes, DateTimeOffset now)
    {
        var existing = episode.Files.FirstOrDefault(f => f.Side == side);
        if (existing is null)
        {
            episode.Files.Add(new EpisodeFile
            {
                Side = side,
                RelativePath = relativePath,
                SizeBytes = sizeBytes,
                ObservedAt = now,
            });
        }
        else
        {
            existing.RelativePath = relativePath;
            existing.SizeBytes = sizeBytes;
            existing.ObservedAt = now;
        }
    }

    private static void RemoveFile(Episode episode, FileSide side)
    {
        var existing = episode.Files.FirstOrDefault(f => f.Side == side);
        if (existing is not null)
        {
            episode.Files.Remove(existing);
        }
    }
}
