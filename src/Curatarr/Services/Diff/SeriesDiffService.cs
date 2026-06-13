using Curatarr.Configuration;
using Curatarr.Data;
using Curatarr.Models;
using Curatarr.Services.Destination;
using Curatarr.Services.Subtitle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Curatarr.Services.Diff;

public record SeriesDiffRow(
    string Title,
    int? SonarrId,
    string? SourceFolder,
    string? DestinationFolder,
    int SourceEpisodes,
    int DestinationEpisodes,
    int OrphanedFiles,
    int MissingSubtitles,
    int OriginalSubtitles,
    int DownloadedSubtitles,
    int ExcessiveSubtitles)
{
    public bool InSource => SourceFolder is not null;
    public bool InDestination => DestinationFolder is not null;
    public int MissingEpisodes => Math.Max(0, SourceEpisodes - DestinationEpisodes);
    public bool IsOrphanedFolder => InDestination && !InSource;
    public bool IsMissingInDestination => InSource && !InDestination;
    public bool IsOk => InSource && InDestination
        && MissingEpisodes == 0
        && OrphanedFiles == 0
        && MissingSubtitles == 0
        && ExcessiveSubtitles == 0;
}

public record SeriesDetail(
    string Title,
    int SonarrId,
    IReadOnlyList<EpisodeDiffRow> Episodes,
    IReadOnlyList<OrphanedFileRow> OrphanedFiles);

public record SubtitleEntry(string Suffix, SubtitleOrigin Origin);

public record EpisodeDiffRow(
    int SonarrId,
    int SeasonNumber,
    int EpisodeNumber,
    string Title,
    string? SourceFile,
    string? DestinationFile,
    IReadOnlyList<SubtitleEntry> SourceSubtitles,
    IReadOnlyList<SubtitleEntry> DestinationSubtitles)
{
    public bool HasSource => SourceFile is not null;
    public bool HasDestination => DestinationFile is not null;
    public IReadOnlyList<SubtitleEntry> MissingSubtitles =>
        HasSource && HasDestination
            ? [.. SourceSubtitles.Where(src => !DestinationSubtitles.Any(dst =>
                dst.Suffix.Equals(src.Suffix, StringComparison.OrdinalIgnoreCase)))]
            : [];

    public IReadOnlyList<SubtitleEntry> ExcessiveSubtitles
    {
        get
        {
            if (!HasDestination) return [];
            var destSet = DestinationSubtitles
                .Select(s => s.Suffix)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return [.. DestinationSubtitles
                .Where(s => s.Origin == SubtitleOrigin.Downloaded)
                .Where(s =>
                {
                    var original = SubtitleEquivalence.GetOriginalEquivalent(s.Suffix);
                    return original is not null && destSet.Contains(original);
                })];
        }
    }
}

public record OrphanedFileRow(string RelativePath, long SizeBytes);

public class SeriesDiffService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    DestinationScanner scanner,
    IOptions<SubtitleOptions> subtitleOptions)
{
    private readonly SubtitleOptions _subtitleOptions = subtitleOptions.Value;

    public async Task<IReadOnlyList<SeriesDiffRow>> GetSeriesDiffAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var originalSuffixes = _subtitleOptions.Suffixes
            .Where(s => SubtitleOriginClassifier.Classify(s) == SubtitleOrigin.Original)
            .ToArray();
        var downloadedSuffixes = _subtitleOptions.Suffixes
            .Where(s => SubtitleOriginClassifier.Classify(s) == SubtitleOrigin.Downloaded)
            .ToArray();

        var seriesWithCounts = await db.Series
            .Where(s => s.Episodes.Any(e => e.Files.Any(f => f.Side == FileSide.Source)))
            .OrderBy(s => s.Title)
            .Select(s => new
            {
                s.Id,
                s.Title,
                s.SonarrId,
                s.Path,
                SourceEpisodes = s.Episodes.Count(e => e.Files.Any(f => f.Side == FileSide.Source)),
                DestinationEpisodes = s.Episodes.Count(e => e.Files.Any(f => f.Side == FileSide.Destination)),
                OrphanedFiles = s.OrphanedDestinationFiles.Count,
                MissingSubtitles = s.Episodes
                    .Where(e => e.Files.Any(f => f.Side == FileSide.Source) && e.Files.Any(f => f.Side == FileSide.Destination))
                    .SelectMany(e => e.Subtitles.Where(srcSub => srcSub.Side == FileSide.Source))
                    .Count(srcSub => !srcSub.Episode.Subtitles.Any(destSub =>
                        destSub.Side == FileSide.Destination && destSub.Suffix == srcSub.Suffix)),
                OriginalSubtitles = s.Episodes
                    .SelectMany(e => e.Subtitles)
                    .Count(sub => sub.Side == FileSide.Destination && originalSuffixes.Contains(sub.Suffix)),
                DownloadedSubtitles = s.Episodes
                    .SelectMany(e => e.Subtitles)
                    .Count(sub => sub.Side == FileSide.Destination && downloadedSuffixes.Contains(sub.Suffix)),
            })
            .ToListAsync(ct);

        var excessiveBySeriesId = await ComputeExcessiveBySeriesAsync(db, ct);

        var destinationFolders = scanner.GetSeriesFolders()
            .ToDictionary(name => name, StringComparer.OrdinalIgnoreCase);

        var rows = new List<SeriesDiffRow>();
        var matchedDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var series in seriesWithCounts)
        {
            var sourceFolder = string.IsNullOrEmpty(series.Path) ? null : Path.GetFileName(series.Path);
            string? destinationMatch = null;

            if (sourceFolder is not null && destinationFolders.TryGetValue(sourceFolder, out var dest))
            {
                destinationMatch = dest;
                matchedDestinations.Add(dest);
            }

            rows.Add(new SeriesDiffRow(
                series.Title,
                series.SonarrId,
                sourceFolder,
                destinationMatch,
                series.SourceEpisodes,
                series.DestinationEpisodes,
                series.OrphanedFiles,
                series.MissingSubtitles,
                series.OriginalSubtitles,
                series.DownloadedSubtitles,
                excessiveBySeriesId.GetValueOrDefault(series.Id)));
        }

        foreach (var folder in destinationFolders.Values)
        {
            if (matchedDestinations.Contains(folder)) continue;
            rows.Add(new SeriesDiffRow(
                folder,
                SonarrId: null,
                SourceFolder: null,
                DestinationFolder: folder,
                SourceEpisodes: 0,
                DestinationEpisodes: 0,
                OrphanedFiles: 0,
                MissingSubtitles: 0,
                OriginalSubtitles: 0,
                DownloadedSubtitles: 0,
                ExcessiveSubtitles: 0));
        }

        return [.. rows.OrderBy(r => r.Title)];
    }

    private static async Task<Dictionary<int, int>> ComputeExcessiveBySeriesAsync(
        CuratarrDbContext db, CancellationToken ct)
    {
        var destSubs = await db.SubtitleFiles
            .Where(sf => sf.Side == FileSide.Destination)
            .Select(sf => new { SeriesId = sf.Episode.SeriesId, sf.EpisodeId, sf.Suffix })
            .ToListAsync(ct);

        return destSubs
            .GroupBy(x => x.SeriesId)
            .ToDictionary(
                seriesGroup => seriesGroup.Key,
                seriesGroup => seriesGroup
                    .GroupBy(x => x.EpisodeId)
                    .Sum(epGroup =>
                    {
                        var set = epGroup.Select(x => x.Suffix).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        return epGroup.Count(x =>
                        {
                            var original = SubtitleEquivalence.GetOriginalEquivalent(x.Suffix);
                            return original is not null && set.Contains(original);
                        });
                    }));
    }

    public async Task<SeriesDetail?> GetSeriesDetailAsync(int sonarrId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var series = await db.Series
            .Include(s => s.Episodes).ThenInclude(e => e.Files)
            .Include(s => s.Episodes).ThenInclude(e => e.Subtitles)
            .Include(s => s.OrphanedDestinationFiles)
            .FirstOrDefaultAsync(s => s.SonarrId == sonarrId, ct);

        if (series is null) return null;

        var episodes = series.Episodes
            .Where(e => e.SourceFile is not null)
            .OrderBy(e => e.SeasonNumber)
            .ThenBy(e => e.EpisodeNumber)
            .Select(e => new EpisodeDiffRow(
                e.SonarrId,
                e.SeasonNumber,
                e.EpisodeNumber,
                e.Title,
                e.SourceFile is { } sf ? Path.GetFileName(sf.RelativePath) : null,
                e.DestinationFile is { } df ? Path.GetFileName(df.RelativePath) : null,
                [.. e.Subtitles
                    .Where(s => s.Side == FileSide.Source)
                    .Select(s => new SubtitleEntry(s.Suffix, SubtitleOriginClassifier.Classify(s.Suffix)))
                    .OrderBy(x => x.Suffix)],
                [.. e.Subtitles
                    .Where(s => s.Side == FileSide.Destination)
                    .Select(s => new SubtitleEntry(s.Suffix, SubtitleOriginClassifier.Classify(s.Suffix)))
                    .OrderBy(x => x.Suffix)]))
            .ToList();

        var orphans = series.OrphanedDestinationFiles
            .OrderBy(o => o.RelativePath)
            .Select(o => new OrphanedFileRow(o.RelativePath, o.SizeBytes))
            .ToList();

        return new SeriesDetail(series.Title, series.SonarrId, episodes, orphans);
    }
}
