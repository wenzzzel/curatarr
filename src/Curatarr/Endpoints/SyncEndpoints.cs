using Curatarr.Services.Destination;
using Curatarr.Services.Sonarr;

namespace Curatarr.Endpoints;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/sync", async (
            SonarrSyncService sonarrSync,
            DestinationSyncService destinationSync,
            CancellationToken ct) =>
        {
            var sonarrResult = await sonarrSync.SyncAsync(ct);
            var destinationResult = await destinationSync.SyncAsync(ct);

            return Results.Ok(new
            {
                series = sonarrResult.SeriesCount,
                episodes = sonarrResult.EpisodeCount,
                destinationSeriesScanned = destinationResult.SeriesScanned,
                destinationFilesMatched = destinationResult.FilesMatched,
            });
        });
    }
}
