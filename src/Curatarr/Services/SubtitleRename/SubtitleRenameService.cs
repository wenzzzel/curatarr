using Curatarr.Configuration;
using Curatarr.Data;
using Curatarr.Models;
using Curatarr.Services.Subtitle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Curatarr.Services.SubtitleRename;

public record SubtitleRenameResult(int FilesRenamed, int RenameFailures);

public class SubtitleRenameService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    IOptions<SeriesDestinationOptions> destinationOptions,
    ILogger<SubtitleRenameService> logger)
{
    private readonly SeriesDestinationOptions _destinationOptions = destinationOptions.Value;

    public async Task<SubtitleRenameResult> RunAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_destinationOptions.Root))
        {
            return new SubtitleRenameResult(0, 0);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var allSeries = await db.Series
            .Include(s => s.Episodes).ThenInclude(e => e.Subtitles)
            .ToListAsync(ct);

        var renamed = 0;
        var failed = 0;

        foreach (var series in allSeries)
        {
            var leaf = PathHelpers.GetLeafFolder(series.Path);
            if (leaf is null) continue;

            var seriesFolder = Path.Combine(_destinationOptions.Root, leaf);

            foreach (var episode in series.Episodes)
            {
                var existingDestSuffixes = episode.Subtitles
                    .Where(s => s.Side == FileSide.Destination)
                    .Select(s => s.Suffix)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var sub in episode.Subtitles
                    .Where(s => s.Side == FileSide.Destination
                        && s.Origin == SubtitleOrigin.Original
                        && !SubtitleNaming.IsOriginalVariant(s.Suffix))
                    .ToList())
                {
                    var newSuffix = SubtitleNaming.ToOriginalVariant(sub.Suffix);
                    if (existingDestSuffixes.Contains(newSuffix))
                    {
                        logger.LogWarning(
                            "Skipping rename of {Path}: target suffix {Suffix} already tracked for episode {EpisodeId}",
                            sub.RelativePath, newSuffix, episode.Id);
                        continue;
                    }

                    var oldFullPath = Path.Combine(seriesFolder, sub.RelativePath);
                    var newRelativePath = ReplaceSuffix(sub.RelativePath, sub.Suffix, newSuffix);
                    var newFullPath = Path.Combine(seriesFolder, newRelativePath);

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
                        episode.Subtitles.Remove(sub);
                    }
                    else if (outcome == RenameOutcome.Failed)
                    {
                        failed++;
                    }
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return new SubtitleRenameResult(renamed, failed);
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
            logger.LogInformation("Renamed original subtitle {Old} -> {New}", oldPath, newPath);
            return RenameOutcome.Renamed;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to rename original subtitle {Old} -> {New}", oldPath, newPath);
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
