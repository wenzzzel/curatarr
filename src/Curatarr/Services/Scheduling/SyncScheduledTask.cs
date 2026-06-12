using Curatarr.Services.Destination;
using Curatarr.Services.Sonarr;
using Curatarr.Services.Subtitle;

namespace Curatarr.Services.Scheduling;

public static class SyncScheduledTask
{
    public static ScheduledTask Create(TimeSpan interval) => new(
        name: "Sync",
        description: "Pull series and episodes from Sonarr, scan the destination tree, and refresh subtitles.",
        interval: interval,
        action: RunAsync);

    private static async Task<string> RunAsync(IServiceProvider services, CancellationToken ct)
    {
        var sonarr = services.GetRequiredService<SonarrSyncService>();
        var destination = services.GetRequiredService<DestinationSyncService>();
        var subtitles = services.GetRequiredService<SubtitleSyncService>();

        var sonarrResult = await sonarr.SyncAsync(ct);
        var destResult = await destination.SyncAsync(ct);
        var subResult = await subtitles.SyncAsync(ct);

        return $"{sonarrResult.SeriesCount} series, {sonarrResult.EpisodeCount} episodes, " +
               $"{destResult.FilesMatched} destination files ({destResult.OrphanedFiles} orphaned), " +
               $"{subResult.SourceFound}/{subResult.DestinationFound} source/destination subtitles.";
    }
}
