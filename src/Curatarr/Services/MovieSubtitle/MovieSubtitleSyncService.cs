using Curatarr.Configuration;
using Curatarr.Data;
using Curatarr.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Curatarr.Services.MovieSubtitle;

public record MovieSubtitleSyncResult(int SourceFound, int DestinationFound);

public class MovieSubtitleSyncService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    IOptions<MovieSourceOptions> sourceOptions,
    IOptions<MovieDestinationOptions> destinationOptions,
    IOptions<SubtitleOptions> subtitleOptions)
{
    private readonly MovieSourceOptions _sourceOptions = sourceOptions.Value;
    private readonly MovieDestinationOptions _destinationOptions = destinationOptions.Value;
    private readonly SubtitleOptions _subtitleOptions = subtitleOptions.Value;

    public async Task<MovieSubtitleSyncResult> SyncAsync(CancellationToken ct = default)
    {
        if (_subtitleOptions.Suffixes.Count == 0)
        {
            return new MovieSubtitleSyncResult(0, 0);
        }

        var now = DateTimeOffset.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var allMovies = await db.Movies
            .Include(m => m.Files)
            .Include(m => m.Subtitles)
            .ToListAsync(ct);

        var sourceCount = 0;
        var destCount = 0;

        foreach (var movie in allMovies)
        {
            var leaf = string.IsNullOrEmpty(movie.Path) ? null : Path.GetFileName(movie.Path);
            if (leaf is null)
            {
                ClearSubtitlesForMovie(movie);
                continue;
            }

            var sourceFolder = ResolveFolder(_sourceOptions.Root, leaf);
            var destFolder = ResolveFolder(_destinationOptions.Root, leaf);

            sourceCount += SyncSubtitlesForSide(movie, FileSide.Source, sourceFolder, now);
            destCount += SyncSubtitlesForSide(movie, FileSide.Destination, destFolder, now);
        }

        await db.SaveChangesAsync(ct);
        return new MovieSubtitleSyncResult(sourceCount, destCount);
    }

    private static string? ResolveFolder(string? root, string leaf)
    {
        if (string.IsNullOrWhiteSpace(root)) return null;
        var combined = Path.Combine(root, leaf);
        return Directory.Exists(combined) ? combined : null;
    }

    private int SyncSubtitlesForSide(Movie movie, FileSide side, string? movieFolder, DateTimeOffset now)
    {
        var videoFile = movie.Files.FirstOrDefault(f => f.Side == side);
        if (movieFolder is null || videoFile is null)
        {
            ClearSubtitlesForSide(movie, side);
            return 0;
        }

        var videoFullPath = Path.Combine(movieFolder, videoFile.RelativePath);
        var videoDir = Path.GetDirectoryName(videoFullPath);
        var videoStem = Path.GetFileNameWithoutExtension(videoFullPath);
        if (videoDir is null || string.IsNullOrEmpty(videoStem) || !Directory.Exists(videoDir))
        {
            ClearSubtitlesForSide(movie, side);
            return 0;
        }

        var observedSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = 0;

        foreach (var suffix in _subtitleOptions.Suffixes)
        {
            var subtitlePath = Path.Combine(videoDir, videoStem + suffix);
            if (!File.Exists(subtitlePath)) continue;

            var relativePath = Path.GetRelativePath(movieFolder, subtitlePath);
            var size = new FileInfo(subtitlePath).Length;
            UpsertSubtitle(movie, side, suffix, relativePath, size, now);
            observedSuffixes.Add(suffix);
            count++;
        }

        var stale = movie.Subtitles
            .Where(s => s.Side == side && !observedSuffixes.Contains(s.Suffix))
            .ToList();
        foreach (var sub in stale)
        {
            movie.Subtitles.Remove(sub);
        }

        return count;
    }

    private static void ClearSubtitlesForMovie(Movie movie)
    {
        movie.Subtitles.Clear();
    }

    private static void ClearSubtitlesForSide(Movie movie, FileSide side)
    {
        var toRemove = movie.Subtitles.Where(s => s.Side == side).ToList();
        foreach (var sub in toRemove)
        {
            movie.Subtitles.Remove(sub);
        }
    }

    private static void UpsertSubtitle(Movie movie, FileSide side, string suffix, string relativePath, long sizeBytes, DateTimeOffset now)
    {
        var existing = movie.Subtitles.FirstOrDefault(s =>
            s.Side == side && string.Equals(s.Suffix, suffix, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            movie.Subtitles.Add(new MovieSubtitleFile
            {
                Side = side,
                Suffix = suffix,
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
}
