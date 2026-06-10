using Curatarr.Services.Sonarr;

namespace Curatarr.Endpoints;

public static class SonarrEndpoints
{
    public static void MapSonarrEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sonarr");

        group.MapGet("/ping", async (SonarrClient sonarr, CancellationToken ct) =>
        {
            var status = await sonarr.GetSystemStatusAsync(ct);
            return Results.Ok(status);
        });
    }
}
