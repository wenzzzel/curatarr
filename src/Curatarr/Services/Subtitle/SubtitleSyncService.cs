using Curatarr.Configuration;
using Curatarr.Data;
using Curatarr.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Curatarr.Services.Subtitle;

public record SubtitleSyncResult(int SourceFound, int DestinationFound);

public class SubtitleSyncService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    IOptions<SeriesSourceOptions> sourceOptions,
    IOptions<SeriesDestinationOptions> destinationOptions,
    IOptions<SubtitleOptions> subtitleOptions)
{
    private readonly SeriesSourceOptions _sourceOptions = sourceOptions.Value;
    private readonly SeriesDestinationOptions _destinationOptions = destinationOptions.Value;
    private readonly SubtitleOptions _subtitleOptions = subtitleOptions.Value;

    public async Task<SubtitleSyncResult> SyncAsync(CancellationToken ct = default)
    {
        if (_subtitleOptions.Suffixes.Count == 0)
        {
            return new SubtitleSyncResult(0, 0);
        }

        var now = DateTimeOffset.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var allSeries = await db.Series
            .Include(s => s.Episodes).ThenInclude(e => e.Files)
            .Include(s => s.Episodes).ThenInclude(e => e.Subtitles)
            .ToListAsync(ct);

        var sourceCount = 0;
        var destCount = 0;

        foreach (var series in allSeries)
        {
            var leaf = string.IsNullOrEmpty(series.Path) ? null : Path.GetFileName(series.Path);
            if (leaf is null)
            {
                ClearSubtitlesForSeries(series);
                continue;
            }

            var sourceFolder = ResolveFolder(_sourceOptions.Root, leaf);
            var destFolder = ResolveFolder(_destinationOptions.Root, leaf);

            foreach (var episode in series.Episodes)
            {
                sourceCount += SyncSubtitlesForSide(episode, FileSide.Source, sourceFolder, now);
                destCount += SyncSubtitlesForSide(episode, FileSide.Destination, destFolder, now);
            }
        }

        await db.SaveChangesAsync(ct);
        return new SubtitleSyncResult(sourceCount, destCount);
    }

    private static string? ResolveFolder(string? root, string leaf)
    {
        if (string.IsNullOrWhiteSpace(root)) return null;
        var combined = Path.Combine(root, leaf);
        return Directory.Exists(combined) ? combined : null;
    }

    private int SyncSubtitlesForSide(Episode episode, FileSide side, string? seriesFolder, DateTimeOffset now)
    {
        var videoFile = episode.Files.FirstOrDefault(f => f.Side == side);
        if (seriesFolder is null || videoFile is null)
        {
            ClearSubtitlesForSide(episode, side);
            return 0;
        }

        var videoFullPath = Path.Combine(seriesFolder, videoFile.RelativePath);
        var videoDir = Path.GetDirectoryName(videoFullPath);
        var videoStem = Path.GetFileNameWithoutExtension(videoFullPath);
        if (videoDir is null || string.IsNullOrEmpty(videoStem) || !Directory.Exists(videoDir))
        {
            ClearSubtitlesForSide(episode, side);
            return 0;
        }

        var observedSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = 0;

        foreach (var suffix in _subtitleOptions.Suffixes)
        {
            var subtitlePath = Path.Combine(videoDir, videoStem + suffix);
            if (!File.Exists(subtitlePath)) continue;

            var relativePath = Path.GetRelativePath(seriesFolder, subtitlePath);
            var size = new FileInfo(subtitlePath).Length;
            UpsertSubtitle(episode, side, suffix, relativePath, size, now);
            observedSuffixes.Add(suffix);
            count++;
        }

        var stale = episode.Subtitles
            .Where(s => s.Side == side && !observedSuffixes.Contains(s.Suffix))
            .ToList();
        foreach (var sub in stale)
        {
            episode.Subtitles.Remove(sub);
        }

        return count;
    }

    private static void ClearSubtitlesForSeries(Series series)
    {
        foreach (var episode in series.Episodes)
        {
            episode.Subtitles.Clear();
        }
    }

    private static void ClearSubtitlesForSide(Episode episode, FileSide side)
    {
        var toRemove = episode.Subtitles.Where(s => s.Side == side).ToList();
        foreach (var sub in toRemove)
        {
            episode.Subtitles.Remove(sub);
        }
    }

    private static void UpsertSubtitle(Episode episode, FileSide side, string suffix, string relativePath, long sizeBytes, DateTimeOffset now)
    {
        var existing = episode.Subtitles.FirstOrDefault(s =>
            s.Side == side && string.Equals(s.Suffix, suffix, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            episode.Subtitles.Add(new SubtitleFile
            {
                Side = side,
                Suffix = suffix,
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
}
