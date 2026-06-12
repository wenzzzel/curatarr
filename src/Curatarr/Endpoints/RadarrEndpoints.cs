using Curatarr.Services.Radarr;

namespace Curatarr.Endpoints;

public static class RadarrEndpoints
{
    public static void MapRadarrEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/radarr");

        group.MapGet("/ping", async (RadarrClient radarr, CancellationToken ct) =>
        {
            var status = await radarr.GetSystemStatusAsync(ct);
            return Results.Ok(status);
        });
    }
}
