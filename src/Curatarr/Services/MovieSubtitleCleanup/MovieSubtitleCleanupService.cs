using Curatarr.Configuration;
using Curatarr.Data;
using Curatarr.Models;
using Curatarr.Services.Subtitle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Curatarr.Services.MovieSubtitleCleanup;

public record MovieSubtitleCleanupResult(int FilesDeleted, int DeletionFailures);

public class MovieSubtitleCleanupService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    IOptions<MovieDestinationOptions> destinationOptions,
    ILogger<MovieSubtitleCleanupService> logger)
{
    private readonly MovieDestinationOptions _destinationOptions = destinationOptions.Value;

    public async Task<MovieSubtitleCleanupResult> RunAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_destinationOptions.Root))
        {
            return new MovieSubtitleCleanupResult(0, 0);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var allMovies = await db.Movies
            .Include(m => m.Subtitles)
            .ToListAsync(ct);

        var deleted = 0;
        var failed = 0;

        foreach (var movie in allMovies)
        {
            var leaf = string.IsNullOrEmpty(movie.Path) ? null : Path.GetFileName(movie.Path);
            if (leaf is null) continue;

            var movieFolder = Path.Combine(_destinationOptions.Root, leaf);

            var destSubs = movie.Subtitles
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

                var excessivePath = Path.Combine(movieFolder, sub.RelativePath);
                var keeperPath = Path.Combine(movieFolder, keeper.RelativePath);

                var outcome = TryDeleteExcessive(excessivePath, keeperPath);
                if (outcome == DeletionOutcome.Removed)
                {
                    movie.Subtitles.Remove(sub);
                    deleted++;
                }
                else if (outcome == DeletionOutcome.StaleDbRow)
                {
                    movie.Subtitles.Remove(sub);
                }
                else if (outcome == DeletionOutcome.Failed)
                {
                    failed++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return new MovieSubtitleCleanupResult(deleted, failed);
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
                "Skipping excessive movie subtitle {Excessive}: equivalent {Keeper} is missing from disk",
                excessivePath, keeperPath);
            return DeletionOutcome.Skipped;
        }

        try
        {
            File.Delete(excessivePath);
            logger.LogInformation("Deleted excessive movie subtitle {Path}", excessivePath);
            return DeletionOutcome.Removed;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete excessive movie subtitle {Path}", excessivePath);
            return DeletionOutcome.Failed;
        }
    }

    private enum DeletionOutcome { Removed, StaleDbRow, Skipped, Failed }
}
