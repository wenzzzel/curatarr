using Curatarr.Data;
using Curatarr.Models;
using Curatarr.Services.Destination;
using Curatarr.Services.Subtitle;
using Microsoft.EntityFrameworkCore;

namespace Curatarr.Services.Diff;

public record SeriesDiffRow(
    string Title,
    int? SonarrId,
    string? SourceFolder,
    string? DestinationFolder,
    int SourceEpisodes,
    int DestinationEpisodes,
    int OkEpisodes,
    int EpisodesWithoutOriginalSubs,
    int OrphanedFiles,
    int MissingSubtitles,
    int OriginalSubtitles,
    int DownloadedSubtitles,
    int UnknownSubtitles,
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
        && ExcessiveSubtitles == 0
        && (OriginalSubtitles > 0 || UnknownSubtitles > 0);
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
                SubtitleNaming.DestinationSatisfiesSource(dst.Suffix, src.Suffix)))]
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
                .Where(s => SubtitleEquivalence.GetOriginalEquivalents(s.Suffix)
                    .Any(eq => destSet.Contains(eq)))];
        }
    }
}

public record OrphanedFileRow(string RelativePath, long SizeBytes);

public class SeriesDiffService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    DestinationScanner scanner)
{
    public async Task<IReadOnlyList<SeriesDiffRow>> GetSeriesDiffAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

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
                OriginalSubtitles = s.Episodes
                    .SelectMany(e => e.Subtitles)
                    .Count(sub => sub.Side == FileSide.Destination && sub.Origin == SubtitleOrigin.Original),
                DownloadedSubtitles = s.Episodes
                    .SelectMany(e => e.Subtitles)
                    .Count(sub => sub.Side == FileSide.Destination && sub.Origin == SubtitleOrigin.Downloaded),
                UnknownSubtitles = s.Episodes
                    .SelectMany(e => e.Subtitles)
                    .Count(sub => sub.Side == FileSide.Destination && sub.Origin == SubtitleOrigin.Unknown),
            })
            .ToListAsync(ct);

        var episodeAggregatesBySeriesId = await ComputeEpisodeAggregatesBySeriesAsync(db, ct);

        var destinationFolders = scanner.GetSeriesFolders()
            .ToDictionary(name => name, StringComparer.OrdinalIgnoreCase);

        var rows = new List<SeriesDiffRow>();
        var matchedDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var series in seriesWithCounts)
        {
            var sourceFolder = PathHelpers.GetLeafFolder(series.Path);
            string? destinationMatch = null;

            if (sourceFolder is not null && destinationFolders.TryGetValue(sourceFolder, out var dest))
            {
                destinationMatch = dest;
                matchedDestinations.Add(dest);
            }

            var aggregates = episodeAggregatesBySeriesId.GetValueOrDefault(series.Id) ?? EpisodeAggregates.Empty;
            rows.Add(new SeriesDiffRow(
                series.Title,
                series.SonarrId,
                sourceFolder,
                destinationMatch,
                series.SourceEpisodes,
                series.DestinationEpisodes,
                aggregates.OkEpisodes,
                aggregates.EpisodesWithoutOriginalSubs,
                series.OrphanedFiles,
                aggregates.MissingSubtitles,
                series.OriginalSubtitles,
                series.DownloadedSubtitles,
                series.UnknownSubtitles,
                aggregates.ExcessiveSubtitles));
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
                OkEpisodes: 0,
                EpisodesWithoutOriginalSubs: 0,
                OrphanedFiles: 0,
                MissingSubtitles: 0,
                OriginalSubtitles: 0,
                DownloadedSubtitles: 0,
                UnknownSubtitles: 0,
                ExcessiveSubtitles: 0));
        }

        return [.. rows.OrderBy(r => r.Title)];
    }

    private sealed record EpisodeAggregates(int OkEpisodes, int EpisodesWithoutOriginalSubs, int MissingSubtitles, int ExcessiveSubtitles)
    {
        public static EpisodeAggregates Empty { get; } = new(0, 0, 0, 0);
    }

    private static async Task<Dictionary<int, EpisodeAggregates>> ComputeEpisodeAggregatesBySeriesAsync(
        CuratarrDbContext db, CancellationToken ct)
    {
        var episodes = await db.Episodes
            .Select(e => new
            {
                e.SeriesId,
                HasSource = e.Files.Any(f => f.Side == FileSide.Source),
                HasDestination = e.Files.Any(f => f.Side == FileSide.Destination),
                SourceSubs = e.Subtitles
                    .Where(s => s.Side == FileSide.Source)
                    .Select(s => s.Suffix).ToList(),
                DestSubs = e.Subtitles
                    .Where(s => s.Side == FileSide.Destination)
                    .Select(s => new { s.Suffix, s.Origin }).ToList(),
            })
            .ToListAsync(ct);

        var result = new Dictionary<int, EpisodeAggregates>();
        foreach (var seriesGroup in episodes.GroupBy(e => e.SeriesId))
        {
            var ok = 0;
            var withoutOriginal = 0;
            var missing = 0;
            var excessive = 0;
            foreach (var ep in seriesGroup)
            {
                var destSetCi = ep.DestSubs.Select(s => s.Suffix).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var epExcessive = ep.DestSubs.Count(s =>
                    SubtitleEquivalence.GetOriginalEquivalents(s.Suffix)
                        .Any(eq => destSetCi.Contains(eq)));
                excessive += epExcessive;

                if (!ep.HasDestination) continue;

                var epHasOriginal = ep.DestSubs.Any(s => s.Origin == SubtitleOrigin.Original);
                var epHasUnknown = ep.DestSubs.Any(s => s.Origin == SubtitleOrigin.Unknown);
                if (!epHasOriginal && !epHasUnknown) withoutOriginal++;

                if (!ep.HasSource) continue;

                var epMissing = ep.SourceSubs.Count(src =>
                    !ep.DestSubs.Any(dst => SubtitleNaming.DestinationSatisfiesSource(dst.Suffix, src)));
                missing += epMissing;

                if (epMissing == 0 && epExcessive == 0 && (epHasOriginal || epHasUnknown))
                {
                    ok++;
                }
            }
            result[seriesGroup.Key] = new EpisodeAggregates(ok, withoutOriginal, missing, excessive);
        }
        return result;
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
                    .Select(s => new SubtitleEntry(s.Suffix, s.Origin))
                    .OrderBy(x => x.Suffix)],
                [.. e.Subtitles
                    .Where(s => s.Side == FileSide.Destination)
                    .Select(s => new SubtitleEntry(s.Suffix, s.Origin))
                    .OrderBy(x => x.Suffix)]))
            .ToList();

        var orphans = series.OrphanedDestinationFiles
            .OrderBy(o => o.RelativePath)
            .Select(o => new OrphanedFileRow(o.RelativePath, o.SizeBytes))
            .ToList();

        return new SeriesDetail(series.Title, series.SonarrId, episodes, orphans);
    }
}
