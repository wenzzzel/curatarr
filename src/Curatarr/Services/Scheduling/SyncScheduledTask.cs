using Curatarr.Services.Bazarr;
using Curatarr.Services.Destination;
using Curatarr.Services.MovieDestination;
using Curatarr.Services.MovieSubtitle;
using Curatarr.Services.MovieSubtitleRename;
using Curatarr.Services.Radarr;
using Curatarr.Services.Sonarr;
using Curatarr.Services.Subtitle;
using Curatarr.Services.SubtitleRename;

namespace Curatarr.Services.Scheduling;

public static class SyncScheduledTask
{
    public static ScheduledTask Create(TimeSpan interval) => new(
        name: "Sync",
        description: "Pull series and movies from Sonarr and Radarr, scan the destination trees, refresh subtitles, and classify subtitle origin via Bazarr.",
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
        var bazarr = services.GetRequiredService<BazarrSyncService>();
        var seriesRename = services.GetRequiredService<SubtitleRenameService>();
        var movieRename = services.GetRequiredService<MovieSubtitleRenameService>();

        var sonarrResult = await sonarr.SyncAsync(ct);
        var radarrResult = await radarr.SyncAsync(ct);
        var destResult = await destination.SyncAsync(ct);
        var movieDestResult = await movieDestination.SyncAsync(ct);
        var subResult = await subtitles.SyncAsync(ct);
        var movieSubResult = await movieSubtitles.SyncAsync(ct);
        var bazarrResult = await bazarr.SyncAsync(ct);
        var seriesRenameResult = await seriesRename.RunAsync(ct);
        var movieRenameResult = await movieRename.RunAsync(ct);

        var bazarrSummary = bazarrResult.BazarrReachable
            ? $"Bazarr: classified {bazarrResult.SeriesSubtitlesUpdated} series + {bazarrResult.MovieSubtitlesUpdated} movie subtitle rows."
            : "Bazarr: unreachable; all subtitle origins set to Unknown.";

        return $"{sonarrResult.SeriesCount} series, {sonarrResult.EpisodeCount} episodes, " +
               $"{radarrResult.MovieCount} movies, " +
               $"{destResult.FilesMatched} series destination files ({destResult.OrphanedFiles} orphaned), " +
               $"{movieDestResult.FilesMatched} movie destination files ({movieDestResult.OrphanedFiles} orphaned), " +
               $"{subResult.SourceFound}/{subResult.DestinationFound} series source/destination subtitles, " +
               $"{movieSubResult.SourceFound}/{movieSubResult.DestinationFound} movie source/destination subtitles. " +
               bazarrSummary + " " +
               $"Renamed originals: {seriesRenameResult.FilesRenamed} series ({seriesRenameResult.RenameFailures} failed), " +
               $"{movieRenameResult.FilesRenamed} movie ({movieRenameResult.RenameFailures} failed).";
    }
}
