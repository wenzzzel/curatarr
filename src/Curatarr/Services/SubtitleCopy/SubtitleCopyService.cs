using Curatarr.Configuration;
using Curatarr.Data;
using Curatarr.Models;
using Curatarr.Services.Subtitle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Curatarr.Services.SubtitleCopy;

public record SubtitleCopyResult(int FilesCopied, int CopyFailures);

public class SubtitleCopyService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    IOptions<SeriesSourceOptions> sourceOptions,
    IOptions<SeriesDestinationOptions> destinationOptions,
    ILogger<SubtitleCopyService> logger)
{
    private readonly SeriesSourceOptions _sourceOptions = sourceOptions.Value;
    private readonly SeriesDestinationOptions _destinationOptions = destinationOptions.Value;

    public async Task<SubtitleCopyResult> RunAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_sourceOptions.Root) ||
            string.IsNullOrWhiteSpace(_destinationOptions.Root))
        {
            return new SubtitleCopyResult(0, 0);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var allSeries = await db.Series
            .Include(s => s.Episodes).ThenInclude(e => e.Files)
            .Include(s => s.Episodes).ThenInclude(e => e.Subtitles)
            .ToListAsync(ct);

        var copied = 0;
        var failed = 0;

        foreach (var series in allSeries)
        {
            var leaf = PathHelpers.GetLeafFolder(series.Path);
            if (leaf is null) continue;

            var sourceFolder = Path.Combine(_sourceOptions.Root, leaf);
            var destFolder = Path.Combine(_destinationOptions.Root, leaf);

            foreach (var episode in series.Episodes)
            {
                var destVideo = episode.Files.FirstOrDefault(f => f.Side == FileSide.Destination);
                if (destVideo is null) continue;

                var destSuffixes = episode.Subtitles
                    .Where(s => s.Side == FileSide.Destination)
                    .Select(s => s.Suffix)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var destVideoFullPath = Path.Combine(destFolder, destVideo.RelativePath);
                var destDir = Path.GetDirectoryName(destVideoFullPath);
                var destStem = Path.GetFileNameWithoutExtension(destVideoFullPath);
                if (destDir is null || string.IsNullOrEmpty(destStem)) continue;

                foreach (var sub in episode.Subtitles.Where(s => s.Side == FileSide.Source))
                {
                    var destSuffix = sub.Origin == SubtitleOrigin.Original
                        ? SubtitleNaming.ToOriginalVariant(sub.Suffix)
                        : sub.Suffix;

                    if (ShouldSkip(destSuffix, destSuffixes)) continue;

                    var sourcePath = Path.Combine(sourceFolder, sub.RelativePath);
                    var destPath = Path.Combine(destDir, destStem + destSuffix);

                    var outcome = TryCopy(sourcePath, destPath);
                    if (outcome == CopyOutcome.Copied) copied++;
                    else if (outcome == CopyOutcome.Failed) failed++;
                }
            }
        }

        return new SubtitleCopyResult(copied, failed);
    }

    private static bool ShouldSkip(string destSuffix, HashSet<string> destSuffixes)
    {
        if (destSuffixes.Contains(destSuffix)) return true;

        // If we're about to write the .original variant, the legacy bare form may already be on disk.
        if (SubtitleNaming.IsOriginalVariant(destSuffix)
            && destSuffixes.Contains(SubtitleNaming.FromOriginalVariant(destSuffix)))
        {
            return true;
        }

        // For downloaded suffixes (e.g. .en.srt), the original-language equivalent makes this excessive.
        return SubtitleEquivalence.GetOriginalEquivalents(destSuffix)
            .Any(eq => destSuffixes.Contains(eq));
    }

    private CopyOutcome TryCopy(string sourcePath, string destPath)
    {
        if (!File.Exists(sourcePath))
        {
            return CopyOutcome.SkippedMissingSource;
        }
        if (File.Exists(destPath))
        {
            return CopyOutcome.SkippedAlreadyExists;
        }
        try
        {
            File.Copy(sourcePath, destPath);
            logger.LogInformation("Copied subtitle {Source} -> {Dest}", sourcePath, destPath);
            return CopyOutcome.Copied;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to copy subtitle {Source} -> {Dest}", sourcePath, destPath);
            return CopyOutcome.Failed;
        }
    }

    private enum CopyOutcome { Copied, SkippedAlreadyExists, SkippedMissingSource, Failed }
}
