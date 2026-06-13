using Curatarr.Services.MovieSubtitleCleanup;
using Curatarr.Services.SubtitleCleanup;

namespace Curatarr.Services.Scheduling;

public static class ExcessiveSubtitleCleanupScheduledTask
{
    public static ScheduledTask Create(TimeSpan interval) => new(
        name: "Excessive subtitle cleanup",
        description: "Delete destination subtitle files that duplicate an original-language equivalent.",
        interval: interval,
        action: RunAsync);

    private static async Task<string> RunAsync(IServiceProvider services, CancellationToken ct)
    {
        var seriesCleanup = services.GetRequiredService<SubtitleCleanupService>();
        var movieCleanup = services.GetRequiredService<MovieSubtitleCleanupService>();

        var seriesResult = await seriesCleanup.RunAsync(ct);
        var movieResult = await movieCleanup.RunAsync(ct);

        return $"Series: {seriesResult.FilesDeleted} deleted, {seriesResult.DeletionFailures} failed. " +
               $"Movies: {movieResult.FilesDeleted} deleted, {movieResult.DeletionFailures} failed.";
    }
}
