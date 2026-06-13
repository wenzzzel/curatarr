using Curatarr.Configuration;
using Curatarr.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Curatarr.Services.OrphanedFileCleanup;

public record OrphanedFileCleanupResult(int FilesDeleted, int DeletionFailures);

public class OrphanedFileCleanupService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    IOptions<SeriesDestinationOptions> destinationOptions,
    ILogger<OrphanedFileCleanupService> logger)
{
    private readonly SeriesDestinationOptions _destinationOptions = destinationOptions.Value;

    public async Task<OrphanedFileCleanupResult> RunAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_destinationOptions.Root))
        {
            return new OrphanedFileCleanupResult(0, 0);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var allSeries = await db.Series
            .Include(s => s.OrphanedDestinationFiles)
            .ToListAsync(ct);

        var deleted = 0;
        var failed = 0;

        foreach (var series in allSeries)
        {
            var leaf = PathHelpers.GetLeafFolder(series.Path);
            if (leaf is null) continue;

            var seriesFolder = Path.Combine(_destinationOptions.Root, leaf);

            foreach (var orphan in series.OrphanedDestinationFiles.ToList())
            {
                var fullPath = Path.Combine(seriesFolder, orphan.RelativePath);

                if (!File.Exists(fullPath))
                {
                    series.OrphanedDestinationFiles.Remove(orphan);
                    continue;
                }

                try
                {
                    File.Delete(fullPath);
                    series.OrphanedDestinationFiles.Remove(orphan);
                    logger.LogInformation("Deleted orphaned series file {Path}", fullPath);
                    deleted++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete orphaned series file {Path}", fullPath);
                    failed++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return new OrphanedFileCleanupResult(deleted, failed);
    }
}
