using Curatarr.Configuration;
using Curatarr.Data;
using Curatarr.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Curatarr.Services.MovieDestination;

public record MovieDestinationSyncResult(int MoviesScanned, int FilesMatched, int OrphanedFiles);

public class MovieDestinationSyncService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    IOptions<MovieDestinationOptions> options)
{
    private const string DestinationExtension = ".mp4";

    private readonly MovieDestinationOptions _options = options.Value;

    public async Task<MovieDestinationSyncResult> SyncAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Root) || !Directory.Exists(_options.Root))
        {
            return new MovieDestinationSyncResult(0, 0, 0);
        }

        var now = DateTimeOffset.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var allMovies = await db.Movies
            .Include(m => m.Files)
            .Include(m => m.OrphanedDestinationFiles)
            .ToListAsync(ct);

        var destinationFolders = Directory.EnumerateDirectories(_options.Root)
            .Select(p => (Full: p, Leaf: Path.GetFileName(p)))
            .Where(x => !string.IsNullOrEmpty(x.Leaf))
            .ToDictionary(x => x.Leaf, x => x.Full, StringComparer.Ordinal);

        var moviesScanned = 0;
        var filesMatched = 0;
        var orphanedFiles = 0;

        foreach (var movie in allMovies)
        {
            if (movie.Files.All(f => f.Side != FileSide.Source))
            {
                ClearDestinationFiles(movie);
                ClearOrphans(movie);
                continue;
            }

            var leafFolder = string.IsNullOrEmpty(movie.Path) ? null : Path.GetFileName(movie.Path);
            if (leafFolder is null || !destinationFolders.TryGetValue(leafFolder, out var destFolder))
            {
                ClearDestinationFiles(movie);
                ClearOrphans(movie);
                continue;
            }

            moviesScanned++;
            var (matched, orphaned) = SyncMovieDestinationFiles(movie, destFolder, now);
            filesMatched += matched;
            orphanedFiles += orphaned;
        }

        await db.SaveChangesAsync(ct);
        return new MovieDestinationSyncResult(moviesScanned, filesMatched, orphanedFiles);
    }

    private static (int Matched, int Orphaned) SyncMovieDestinationFiles(Movie movie, string destFolder, DateTimeOffset now)
    {
        var sourceFile = movie.Files.FirstOrDefault(f => f.Side == FileSide.Source);
        var sourceStem = sourceFile is null ? null : Path.GetFileNameWithoutExtension(sourceFile.RelativePath);

        var observedOrphans = new HashSet<string>(StringComparer.Ordinal);
        var matched = false;
        var orphanCount = 0;

        foreach (var path in Directory.EnumerateFiles(destFolder, $"*{DestinationExtension}", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(destFolder, path);
            var size = new FileInfo(path).Length;
            var stem = Path.GetFileNameWithoutExtension(path);

            if (sourceStem is not null && string.Equals(stem, sourceStem, StringComparison.Ordinal))
            {
                UpsertFile(movie, FileSide.Destination, relativePath, size, now);
                matched = true;
            }
            else
            {
                UpsertOrphan(movie, relativePath, size, now);
                observedOrphans.Add(relativePath);
                orphanCount++;
            }
        }

        if (!matched)
        {
            RemoveFile(movie, FileSide.Destination);
        }

        var staleOrphans = movie.OrphanedDestinationFiles
            .Where(o => !observedOrphans.Contains(o.RelativePath))
            .ToList();
        foreach (var stale in staleOrphans)
        {
            movie.OrphanedDestinationFiles.Remove(stale);
        }

        return (matched ? 1 : 0, orphanCount);
    }

    private static void ClearDestinationFiles(Movie movie)
    {
        RemoveFile(movie, FileSide.Destination);
    }

    private static void ClearOrphans(Movie movie)
    {
        movie.OrphanedDestinationFiles.Clear();
    }

    private static void UpsertOrphan(Movie movie, string relativePath, long sizeBytes, DateTimeOffset now)
    {
        var existing = movie.OrphanedDestinationFiles
            .FirstOrDefault(o => string.Equals(o.RelativePath, relativePath, StringComparison.Ordinal));
        if (existing is null)
        {
            movie.OrphanedDestinationFiles.Add(new OrphanedMovieFile
            {
                RelativePath = relativePath,
                SizeBytes = sizeBytes,
                ObservedAt = now,
            });
        }
        else
        {
            existing.SizeBytes = sizeBytes;
            existing.ObservedAt = now;
        }
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
