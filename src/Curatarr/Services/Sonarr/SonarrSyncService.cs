using Curatarr.Data;
using Curatarr.Models;
using Microsoft.EntityFrameworkCore;

namespace Curatarr.Services.Sonarr;

public class SonarrSyncService(
    SonarrClient client,
    IDbContextFactory<CuratarrDbContext> dbFactory)
{
    public async Task<int> SyncSeriesAsync(CancellationToken ct = default)
    {
        var incoming = await client.GetSeriesAsync(ct);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = await db.Series.ToDictionaryAsync(s => s.SonarrId, ct);
        var now = DateTimeOffset.UtcNow;

        foreach (var dto in incoming)
        {
            if (existing.TryGetValue(dto.Id, out var series))
            {
                series.Title = dto.Title;
                series.Path = dto.Path;
                series.LastSyncedAt = now;
            }
            else
            {
                db.Series.Add(new Series
                {
                    SonarrId = dto.Id,
                    Title = dto.Title,
                    Path = dto.Path,
                    LastSyncedAt = now,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return incoming.Count;
    }
}
