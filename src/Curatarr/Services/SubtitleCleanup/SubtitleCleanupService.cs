using Curatarr.Configuration;
using Curatarr.Data;
using Curatarr.Models;
using Curatarr.Services.Subtitle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Curatarr.Services.SubtitleCleanup;

public record SubtitleCleanupResult(int FilesDeleted, int DeletionFailures);

public class SubtitleCleanupService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    IOptions<SeriesDestinationOptions> destinationOptions,
    ILogger<SubtitleCleanupService> logger)
{
    private readonly SeriesDestinationOptions _destinationOptions = destinationOptions.Value;

    public async Task<SubtitleCleanupResult> RunAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_destinationOptions.Root))
        {
            return new SubtitleCleanupResult(0, 0);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var allSeries = await db.Series
            .Include(s => s.Episodes).ThenInclude(e => e.Subtitles)
            .ToListAsync(ct);

        var deleted = 0;
        var failed = 0;

        foreach (var series in allSeries)
        {
            var leaf = PathHelpers.GetLeafFolder(series.Path);
            if (leaf is null) continue;

            var seriesFolder = Path.Combine(_destinationOptions.Root, leaf);

            foreach (var episode in series.Episodes)
            {
                var destSubs = episode.Subtitles
                    .Where(s => s.Side == FileSide.Destination)
                    .ToList();
                var destSuffixesToRow = destSubs.ToDictionary(
                    s => s.Suffix,
                    s => s,
                    StringComparer.OrdinalIgnoreCase);

                foreach (var sub in destSubs)
                {
                    var originalSuffix = SubtitleEquivalence.GetOriginalEquivalent(sub.Suffix);
                    if (originalSuffix is null) continue;
                    if (!destSuffixesToRow.TryGetValue(originalSuffix, out var keeper)) continue;

                    var excessivePath = Path.Combine(seriesFolder, sub.RelativePath);
                    var keeperPath = Path.Combine(seriesFolder, keeper.RelativePath);

                    var outcome = TryDeleteExcessive(excessivePath, keeperPath);
                    if (outcome == DeletionOutcome.Removed)
                    {
                        episode.Subtitles.Remove(sub);
                        deleted++;
                    }
                    else if (outcome == DeletionOutcome.StaleDbRow)
                    {
                        episode.Subtitles.Remove(sub);
                    }
                    else if (outcome == DeletionOutcome.Failed)
                    {
                        failed++;
                    }
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return new SubtitleCleanupResult(deleted, failed);
    }

    private DeletionOutcome TryDeleteExcessive(string excessivePath, string keeperPath)
    {
        if (!File.Exists(excessivePath))
        {
            return DeletionOutcome.StaleDbRow;
        }

        if (!File.Exists(keeperPath))
        {
            logger.LogWarning(
                "Skipping excessive subtitle {Excessive}: equivalent {Keeper} is missing from disk",
                excessivePath, keeperPath);
            return DeletionOutcome.Skipped;
        }

        try
        {
            File.Delete(excessivePath);
            logger.LogInformation("Deleted excessive subtitle {Path}", excessivePath);
            return DeletionOutcome.Removed;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete excessive subtitle {Path}", excessivePath);
            return DeletionOutcome.Failed;
        }
    }

    private enum DeletionOutcome { Removed, StaleDbRow, Skipped, Failed }
}
