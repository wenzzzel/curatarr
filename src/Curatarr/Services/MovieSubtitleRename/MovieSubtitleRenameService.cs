using Curatarr.Configuration;
using Curatarr.Data;
using Curatarr.Models;
using Curatarr.Services.Subtitle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Curatarr.Services.MovieSubtitleRename;

public record MovieSubtitleRenameResult(int FilesRenamed, int RenameFailures);

public class MovieSubtitleRenameService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    IOptions<MovieDestinationOptions> destinationOptions,
    ILogger<MovieSubtitleRenameService> logger)
{
    private readonly MovieDestinationOptions _destinationOptions = destinationOptions.Value;

    public async Task<MovieSubtitleRenameResult> RunAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_destinationOptions.Root))
        {
            return new MovieSubtitleRenameResult(0, 0);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var allMovies = await db.Movies
            .Include(m => m.Subtitles)
            .ToListAsync(ct);

        var renamed = 0;
        var failed = 0;

        foreach (var movie in allMovies)
        {
            var leaf = PathHelpers.GetLeafFolder(movie.Path);
            if (leaf is null) continue;

            var movieFolder = Path.Combine(_destinationOptions.Root, leaf);

            var existingDestSuffixes = movie.Subtitles
                .Where(s => s.Side == FileSide.Destination)
                .Select(s => s.Suffix)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var sub in movie.Subtitles
                .Where(s => s.Side == FileSide.Destination
                    && s.Origin == SubtitleOrigin.Original
                    && !SubtitleNaming.IsOriginalVariant(s.Suffix))
                .ToList())
            {
                var newSuffix = SubtitleNaming.ToOriginalVariant(sub.Suffix);
                if (existingDestSuffixes.Contains(newSuffix))
                {
                    logger.LogWarning(
                        "Skipping rename of {Path}: target suffix {Suffix} already tracked for movie {MovieId}",
                        sub.RelativePath, newSuffix, movie.Id);
                    continue;
                }

                var oldFullPath = Path.Combine(movieFolder, sub.RelativePath);
                var newRelativePath = ReplaceSuffix(sub.RelativePath, sub.Suffix, newSuffix);
                var newFullPath = Path.Combine(movieFolder, newRelativePath);

                var outcome = TryRename(oldFullPath, newFullPath);
                if (outcome == RenameOutcome.Renamed)
                {
                    sub.Suffix = newSuffix;
                    sub.RelativePath = newRelativePath;
                    existingDestSuffixes.Add(newSuffix);
                    renamed++;
                }
                else if (outcome == RenameOutcome.StaleDbRow)
                {
                    movie.Subtitles.Remove(sub);
                }
                else if (outcome == RenameOutcome.Failed)
                {
                    failed++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return new MovieSubtitleRenameResult(renamed, failed);
    }

    private RenameOutcome TryRename(string oldPath, string newPath)
    {
        if (!File.Exists(oldPath))
        {
            return RenameOutcome.StaleDbRow;
        }
        if (File.Exists(newPath))
        {
            logger.LogWarning("Skipping rename of {Old}: target {New} already exists on disk", oldPath, newPath);
            return RenameOutcome.Skipped;
        }
        try
        {
            File.Move(oldPath, newPath);
            logger.LogInformation("Renamed original movie subtitle {Old} -> {New}", oldPath, newPath);
            return RenameOutcome.Renamed;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to rename original movie subtitle {Old} -> {New}", oldPath, newPath);
            return RenameOutcome.Failed;
        }
    }

    private static string ReplaceSuffix(string relativePath, string oldSuffix, string newSuffix)
    {
        if (relativePath.EndsWith(oldSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(relativePath.AsSpan(0, relativePath.Length - oldSuffix.Length), newSuffix);
        }
        return relativePath;
    }

    private enum RenameOutcome { Renamed, StaleDbRow, Skipped, Failed }
}
