using Curatarr.Services.MovieOrphanedFileCleanup;
using Curatarr.Services.OrphanedFileCleanup;

namespace Curatarr.Services.Scheduling;

public static class OrphanedFileCleanupScheduledTask
{
    public static ScheduledTask Create(TimeSpan interval) => new(
        name: "Orphaned file cleanup",
        description: "Delete destination video files that no longer have a matching source file.",
        interval: interval,
        action: RunAsync);

    private static async Task<string> RunAsync(IServiceProvider services, CancellationToken ct)
    {
        var seriesCleanup = services.GetRequiredService<OrphanedFileCleanupService>();
        var movieCleanup = services.GetRequiredService<MovieOrphanedFileCleanupService>();

        var seriesResult = await seriesCleanup.RunAsync(ct);
        var movieResult = await movieCleanup.RunAsync(ct);

        return $"Series: {seriesResult.FilesDeleted} deleted, {seriesResult.DeletionFailures} failed. " +
               $"Movies: {movieResult.FilesDeleted} deleted, {movieResult.DeletionFailures} failed.";
    }
}
