using Curatarr.Services.Destination;
using Curatarr.Services.MovieDestination;
using Curatarr.Services.MovieSubtitle;
using Curatarr.Services.Radarr;
using Curatarr.Services.Sonarr;
using Curatarr.Services.Subtitle;

namespace Curatarr.Endpoints;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/sync", async (
            SonarrSyncService sonarrSync,
            RadarrSyncService radarrSync,
            DestinationSyncService destinationSync,
            MovieDestinationSyncService movieDestinationSync,
            SubtitleSyncService subtitleSync,
            MovieSubtitleSyncService movieSubtitleSync,
            CancellationToken ct) =>
        {
            var sonarrResult = await sonarrSync.SyncAsync(ct);
            var radarrResult = await radarrSync.SyncAsync(ct);
            var destinationResult = await destinationSync.SyncAsync(ct);
            var movieDestinationResult = await movieDestinationSync.SyncAsync(ct);
            var subtitleResult = await subtitleSync.SyncAsync(ct);
            var movieSubtitleResult = await movieSubtitleSync.SyncAsync(ct);

            return Results.Ok(new
            {
                series = sonarrResult.SeriesCount,
                seriesRemoved = sonarrResult.RemovedSeriesCount,
                episodes = sonarrResult.EpisodeCount,
                movies = radarrResult.MovieCount,
                moviesRemoved = radarrResult.RemovedCount,
                destinationSeriesScanned = destinationResult.SeriesScanned,
                destinationFilesMatched = destinationResult.FilesMatched,
                destinationOrphanedFiles = destinationResult.OrphanedFiles,
                movieDestinationScanned = movieDestinationResult.MoviesScanned,
                movieDestinationFilesMatched = movieDestinationResult.FilesMatched,
                movieDestinationOrphanedFiles = movieDestinationResult.OrphanedFiles,
                subtitlesSource = subtitleResult.SourceFound,
                subtitlesDestination = subtitleResult.DestinationFound,
                movieSubtitlesSource = movieSubtitleResult.SourceFound,
                movieSubtitlesDestination = movieSubtitleResult.DestinationFound,
            });
        });
    }
}
