using Curatarr.Configuration;
using Curatarr.Data;
using Curatarr.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Curatarr.Services.Destination;

public record DestinationSyncResult(int SeriesScanned, int FilesMatched);

public class DestinationSyncService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    IOptions<DestinationOptions> options)
{
    private const string DestinationExtension = ".mp4";

    private readonly DestinationOptions _options = options.Value;

    public async Task<DestinationSyncResult> SyncAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Root) || !Directory.Exists(_options.Root))
        {
            return new DestinationSyncResult(0, 0);
        }

        var now = DateTimeOffset.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var allSeries = await db.Series
            .Include(s => s.Episodes)
            .ThenInclude(e => e.Files)
            .ToListAsync(ct);

        var destinationFolders = Directory.EnumerateDirectories(_options.Root)
            .Select(p => (Full: p, Leaf: Path.GetFileName(p)))
            .Where(x => !string.IsNullOrEmpty(x.Leaf))
            .ToDictionary(x => x.Leaf!, x => x.Full, StringComparer.OrdinalIgnoreCase);

        var seriesScanned = 0;
        var filesMatched = 0;

        foreach (var series in allSeries)
        {
            var leafFolder = string.IsNullOrEmpty(series.Path) ? null : Path.GetFileName(series.Path);
            if (leafFolder is null || !destinationFolders.TryGetValue(leafFolder, out var destFolder))
            {
                ClearDestinationFiles(series);
                continue;
            }

            seriesScanned++;
            filesMatched += SyncSeriesDestinationFiles(series, destFolder, now);
        }

        await db.SaveChangesAsync(ct);
        return new DestinationSyncResult(seriesScanned, filesMatched);
    }

    private static int SyncSeriesDestinationFiles(Series series, string destFolder, DateTimeOffset now)
    {
        var sourceByStem = series.Episodes
            .Select(e => new { Episode = e, Source = e.Files.FirstOrDefault(f => f.Side == FileSide.Source) })
            .Where(x => x.Source is not null)
            .ToDictionary(
                x => Path.GetFileNameWithoutExtension(x.Source!.RelativePath),
                x => x.Episode,
                StringComparer.OrdinalIgnoreCase);

        var matchedEpisodeIds = new HashSet<int>();
        var matchCount = 0;

        foreach (var path in Directory.EnumerateFiles(destFolder, $"*{DestinationExtension}", SearchOption.AllDirectories))
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            if (!sourceByStem.TryGetValue(stem, out var episode)) continue;

            var relativePath = Path.GetRelativePath(destFolder, path);
            var size = new FileInfo(path).Length;
            UpsertFile(episode, FileSide.Destination, relativePath, size, now);
            matchedEpisodeIds.Add(episode.SonarrId);
            matchCount++;
        }

        foreach (var episode in series.Episodes)
        {
            if (matchedEpisodeIds.Contains(episode.SonarrId)) continue;
            RemoveFile(episode, FileSide.Destination);
        }

        return matchCount;
    }

    private static void ClearDestinationFiles(Series series)
    {
        foreach (var episode in series.Episodes)
        {
            RemoveFile(episode, FileSide.Destination);
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
