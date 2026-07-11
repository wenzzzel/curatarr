using Curatarr.Services.Destination;
using Curatarr.Services.MovieDestination;
using Curatarr.Services.MovieSubtitle;
using Curatarr.Services.Radarr;
using Curatarr.Services.Sonarr;
using Curatarr.Services.Subtitle;

namespace Curatarr.Services.Scheduling;

public static class SyncScheduledTask
{
    public static ScheduledTask Create(TimeSpan interval) => new(
        name: "Refresh state",
        description: "Pull series and movies from Sonarr and Radarr, scan the destination trees, and refresh subtitles.",
        interval: interval,
        action: RunAsync);

    private static async Task<string> RunAsync(IServiceProvider services, CancellationToken ct)
    {
        var sonarr = services.GetRequiredService<SonarrSyncService>();
        var radarr = services.GetRequiredService<RadarrSyncService>();
        var destination = services.GetRequiredService<DestinationSyncService>();
        var movieDestination = services.GetRequiredService<MovieDestinationSyncService>();
        var subtitles = services.GetRequiredService<SubtitleSyncService>();
        var movieSubtitles = services.GetRequiredService<MovieSubtitleSyncService>();

        var sonarrResult = await sonarr.SyncAsync(ct);
        var radarrResult = await radarr.SyncAsync(ct);
        var destResult = await destination.SyncAsync(ct);
        var movieDestResult = await movieDestination.SyncAsync(ct);
        var subResult = await subtitles.SyncAsync(ct);
        var movieSubResult = await movieSubtitles.SyncAsync(ct);

        return $"{sonarrResult.SeriesCount} series ({sonarrResult.RemovedSeriesCount} removed), {sonarrResult.EpisodeCount} episodes, " +
               $"{radarrResult.MovieCount} movies ({radarrResult.RemovedCount} removed), " +
               $"{destResult.FilesMatched} series destination files ({destResult.OrphanedFiles} orphaned), " +
               $"{movieDestResult.FilesMatched} movie destination files ({movieDestResult.OrphanedFiles} orphaned), " +
               $"{subResult.SourceFound}/{subResult.DestinationFound} series source/destination subtitles, " +
               $"{movieSubResult.SourceFound}/{movieSubResult.DestinationFound} movie source/destination subtitles.";
    }
}
