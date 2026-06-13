using Curatarr.Configuration;
using Curatarr.Data;
using Curatarr.Models;
using Curatarr.Services.MovieDestination;
using Curatarr.Services.Subtitle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Curatarr.Services.Diff;

public record MovieDiffRow(
    string Title,
    int? RadarrId,
    string? SourceFolder,
    string? DestinationFolder,
    bool HasSourceFile,
    bool HasDestinationFile,
    int OrphanedFiles,
    int MissingSubtitles,
    int OriginalSubtitles,
    int DownloadedSubtitles,
    int ExcessiveSubtitles)
{
    public bool InSource => SourceFolder is not null;
    public bool InDestination => DestinationFolder is not null;
    public bool IsMissingDestination => HasSourceFile && !HasDestinationFile;
    public bool IsOrphanedFolder => InDestination && !InSource;
    public bool IsMissingInDestination => HasSourceFile && !HasDestinationFile;
    public bool IsOk => InSource && InDestination
        && !IsMissingDestination
        && OrphanedFiles == 0
        && MissingSubtitles == 0
        && ExcessiveSubtitles == 0;
}

public record MovieDetail(
    string Title,
    int RadarrId,
    string? SourceFile,
    string? DestinationFile,
    IReadOnlyList<SubtitleEntry> SourceSubtitles,
    IReadOnlyList<SubtitleEntry> DestinationSubtitles,
    IReadOnlyList<OrphanedFileRow> OrphanedFiles)
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

public class MovieDiffService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    MovieDestinationScanner scanner,
    IOptions<SubtitleOptions> subtitleOptions)
{
    private readonly SubtitleOptions _subtitleOptions = subtitleOptions.Value;

    public async Task<IReadOnlyList<MovieDiffRow>> GetMoviesDiffAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var originalSuffixes = _subtitleOptions.Suffixes
            .Where(s => SubtitleOriginClassifier.Classify(s) == SubtitleOrigin.Original)
            .ToArray();
        var downloadedSuffixes = _subtitleOptions.Suffixes
            .Where(s => SubtitleOriginClassifier.Classify(s) == SubtitleOrigin.Downloaded)
            .ToArray();

        var moviesWithCounts = await db.Movies
            .Where(m => m.Files.Any(f => f.Side == FileSide.Source))
            .OrderBy(m => m.Title)
            .Select(m => new
            {
                m.Id,
                m.Title,
                m.RadarrId,
                m.Path,
                HasSourceFile = m.Files.Any(f => f.Side == FileSide.Source),
                HasDestinationFile = m.Files.Any(f => f.Side == FileSide.Destination),
                OrphanedFiles = m.OrphanedDestinationFiles.Count,
                MissingSubtitles =
                    m.Files.Any(f => f.Side == FileSide.Source) && m.Files.Any(f => f.Side == FileSide.Destination)
                        ? m.Subtitles.Count(srcSub => srcSub.Side == FileSide.Source &&
                            !m.Subtitles.Any(destSub => destSub.Side == FileSide.Destination && destSub.Suffix == srcSub.Suffix))
                        : 0,
                OriginalSubtitles = m.Subtitles
                    .Count(sub => sub.Side == FileSide.Destination && originalSuffixes.Contains(sub.Suffix)),
                DownloadedSubtitles = m.Subtitles
                    .Count(sub => sub.Side == FileSide.Destination && downloadedSuffixes.Contains(sub.Suffix)),
            })
            .ToListAsync(ct);

        var excessiveByMovieId = await ComputeExcessiveByMovieAsync(db, ct);

        var destinationFolders = scanner.GetMovieFolders()
            .ToDictionary(name => name, StringComparer.Ordinal);

        var rows = new List<MovieDiffRow>();
        var matchedDestinations = new HashSet<string>(StringComparer.Ordinal);

        foreach (var movie in moviesWithCounts)
        {
            var sourceFolder = string.IsNullOrEmpty(movie.Path) ? null : Path.GetFileName(movie.Path);
            string? destinationMatch = null;

            if (sourceFolder is not null && destinationFolders.TryGetValue(sourceFolder, out var dest))
            {
                destinationMatch = dest;
                matchedDestinations.Add(dest);
            }

            rows.Add(new MovieDiffRow(
                movie.Title,
                movie.RadarrId,
                sourceFolder,
                destinationMatch,
                movie.HasSourceFile,
                movie.HasDestinationFile,
                movie.OrphanedFiles,
                movie.MissingSubtitles,
                movie.OriginalSubtitles,
                movie.DownloadedSubtitles,
                excessiveByMovieId.GetValueOrDefault(movie.Id)));
        }

        foreach (var folder in destinationFolders.Values)
        {
            if (matchedDestinations.Contains(folder)) continue;
            rows.Add(new MovieDiffRow(
                folder,
                RadarrId: null,
                SourceFolder: null,
                DestinationFolder: folder,
                HasSourceFile: false,
                HasDestinationFile: false,
                OrphanedFiles: 0,
                MissingSubtitles: 0,
                OriginalSubtitles: 0,
                DownloadedSubtitles: 0,
                ExcessiveSubtitles: 0));
        }

        return [.. rows.OrderBy(r => r.Title)];
    }

    private static async Task<Dictionary<int, int>> ComputeExcessiveByMovieAsync(
        CuratarrDbContext db, CancellationToken ct)
    {
        var destSubs = await db.MovieSubtitleFiles
            .Where(sf => sf.Side == FileSide.Destination)
            .Select(sf => new { sf.MovieId, sf.Suffix })
            .ToListAsync(ct);

        return destSubs
            .GroupBy(x => x.MovieId)
            .ToDictionary(
                movieGroup => movieGroup.Key,
                movieGroup =>
                {
                    var set = movieGroup.Select(x => x.Suffix).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    return movieGroup.Count(x =>
                    {
                        var original = SubtitleEquivalence.GetOriginalEquivalent(x.Suffix);
                        return original is not null && set.Contains(original);
                    });
                });
    }

    public async Task<MovieDetail?> GetMovieDetailAsync(int radarrId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var movie = await db.Movies
            .Include(m => m.Files)
            .Include(m => m.Subtitles)
            .Include(m => m.OrphanedDestinationFiles)
            .FirstOrDefaultAsync(m => m.RadarrId == radarrId, ct);

        if (movie is null) return null;

        var sourceSubs = movie.Subtitles
            .Where(s => s.Side == FileSide.Source)
            .Select(s => new SubtitleEntry(s.Suffix, SubtitleOriginClassifier.Classify(s.Suffix)))
            .OrderBy(x => x.Suffix)
            .ToList();

        var destSubs = movie.Subtitles
            .Where(s => s.Side == FileSide.Destination)
            .Select(s => new SubtitleEntry(s.Suffix, SubtitleOriginClassifier.Classify(s.Suffix)))
            .OrderBy(x => x.Suffix)
            .ToList();

        var orphans = movie.OrphanedDestinationFiles
            .OrderBy(o => o.RelativePath)
            .Select(o => new OrphanedFileRow(o.RelativePath, o.SizeBytes))
            .ToList();

        return new MovieDetail(
            movie.Title,
            movie.RadarrId,
            movie.SourceFile is { } sf ? Path.GetFileName(sf.RelativePath) : null,
            movie.DestinationFile is { } df ? Path.GetFileName(df.RelativePath) : null,
            sourceSubs,
            destSubs,
            orphans);
    }
}
