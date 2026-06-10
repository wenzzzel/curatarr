using Curatarr.Services.Destination;
using Curatarr.Services.Sonarr;
using Curatarr.Services.Subtitle;

namespace Curatarr.Endpoints;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/sync", async (
            SonarrSyncService sonarrSync,
            DestinationSyncService destinationSync,
            SubtitleSyncService subtitleSync,
            CancellationToken ct) =>
        {
            var sonarrResult = await sonarrSync.SyncAsync(ct);
            var destinationResult = await destinationSync.SyncAsync(ct);
            var subtitleResult = await subtitleSync.SyncAsync(ct);

            return Results.Ok(new
            {
                series = sonarrResult.SeriesCount,
                episodes = sonarrResult.EpisodeCount,
                destinationSeriesScanned = destinationResult.SeriesScanned,
                destinationFilesMatched = destinationResult.FilesMatched,
                destinationOrphanedFiles = destinationResult.OrphanedFiles,
                subtitlesSource = subtitleResult.SourceFound,
                subtitlesDestination = subtitleResult.DestinationFound,
            });
        });
    }
}
