using Curatarr.Configuration;
using Curatarr.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Curatarr.Services.MovieOrphanedFileCleanup;

public record MovieOrphanedFileCleanupResult(int FilesDeleted, int DeletionFailures);

public class MovieOrphanedFileCleanupService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    IOptions<MovieDestinationOptions> destinationOptions,
    ILogger<MovieOrphanedFileCleanupService> logger)
{
    private readonly MovieDestinationOptions _destinationOptions = destinationOptions.Value;

    public async Task<MovieOrphanedFileCleanupResult> RunAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_destinationOptions.Root))
        {
            return new MovieOrphanedFileCleanupResult(0, 0);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var allMovies = await db.Movies
            .Include(m => m.OrphanedDestinationFiles)
            .ToListAsync(ct);

        var deleted = 0;
        var failed = 0;

        foreach (var movie in allMovies)
        {
            var leaf = string.IsNullOrEmpty(movie.Path) ? null : Path.GetFileName(movie.Path);
            if (leaf is null) continue;

            var movieFolder = Path.Combine(_destinationOptions.Root, leaf);

            foreach (var orphan in movie.OrphanedDestinationFiles.ToList())
            {
                var fullPath = Path.Combine(movieFolder, orphan.RelativePath);

                if (!File.Exists(fullPath))
                {
                    movie.OrphanedDestinationFiles.Remove(orphan);
                    continue;
                }

                try
                {
                    File.Delete(fullPath);
                    movie.OrphanedDestinationFiles.Remove(orphan);
                    logger.LogInformation("Deleted orphaned movie file {Path}", fullPath);
                    deleted++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete orphaned movie file {Path}", fullPath);
                    failed++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return new MovieOrphanedFileCleanupResult(deleted, failed);
    }
}
