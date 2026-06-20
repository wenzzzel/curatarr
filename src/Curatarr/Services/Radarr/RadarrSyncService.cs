using Curatarr.Data;
using Curatarr.Models;
using Microsoft.EntityFrameworkCore;

namespace Curatarr.Services.Radarr;

public record RadarrSyncResult(int MovieCount, int RemovedCount);

public class RadarrSyncService(
    RadarrClient client,
    IDbContextFactory<CuratarrDbContext> dbFactory)
{
    public async Task<RadarrSyncResult> SyncAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var incoming = await client.GetMoviesAsync(ct);
        var existing = await db.Movies
            .Include(m => m.Files)
            .ToDictionaryAsync(m => m.RadarrId, ct);

        foreach (var dto in incoming)
        {
            Movie movie;
            if (existing.TryGetValue(dto.Id, out var found))
            {
                movie = found;
                movie.Title = dto.Title;
                movie.Path = dto.Path;
                movie.LastSyncedAt = now;
            }
            else
            {
                movie = new Movie
                {
                    RadarrId = dto.Id,
                    Title = dto.Title,
                    Path = dto.Path,
                    LastSyncedAt = now,
                };
                db.Movies.Add(movie);
            }

            if (dto.HasFile && dto.MovieFile is { } file)
            {
                UpsertFile(movie, FileSide.Source, file.RelativePath, file.Size, now);
            }
            else
            {
                RemoveFile(movie, FileSide.Source);
            }
        }

        var removedCount = 0;
        if (incoming.Count > 0)
        {
            var incomingIds = incoming.Select(m => m.Id).ToHashSet();
            var stale = existing.Values.Where(m => !incomingIds.Contains(m.RadarrId)).ToList();
            db.Movies.RemoveRange(stale);
            removedCount = stale.Count;
        }

        await db.SaveChangesAsync(ct);
        return new RadarrSyncResult(incoming.Count, removedCount);
    }

    private static void UpsertFile(Movie movie, FileSide side, string relativePath, long sizeBytes, DateTimeOffset now)
    {
        var existing = movie.Files.FirstOrDefault(f => f.Side == side);
        if (existing is null)
        {
            movie.Files.Add(new MovieFile
            {
                Side = side,
                RelativePath = relativePath,
                SizeBytes = sizeBytes,
                ObservedAt = now,
            });
        }
        else
        {
            existing.RelativePath = relativePath;
            existing.SizeBytes = sizeBytes;
            existing.ObservedAt = now;
        }
    }

    private static void RemoveFile(Movie movie, FileSide side)
    {
        var existing = movie.Files.FirstOrDefault(f => f.Side == side);
        if (existing is not null)
        {
            movie.Files.Remove(existing);
        }
    }
}
