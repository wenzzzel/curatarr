using Curatarr.Configuration;
using Curatarr.Data;
using Curatarr.Models;
using Curatarr.Services.Subtitle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Curatarr.Services.Bazarr;

public record BazarrSyncResult(
    bool BazarrReachable,
    int SeriesSubtitlesUpdated,
    int MovieSubtitlesUpdated);

public class BazarrSyncService(
    IDbContextFactory<CuratarrDbContext> dbFactory,
    BazarrClient client,
    IOptions<BazarrOptions> bazarrOptions,
    ILogger<BazarrSyncService> logger)
{
    private const int BazarrDownloadAction = 1;

    public async Task<BazarrSyncResult> SyncAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bazarrOptions.Value.Url))
        {
            logger.LogInformation("Bazarr URL not configured; marking all subtitle origins as Unknown.");
            return await ResetAllToUnknownAsync(ct);
        }

        IReadOnlySet<(int SeriesId, string RelPath)> downloadedEpisodes;
        IReadOnlySet<(int RadarrId, string RelPath)> downloadedMovies;
        try
        {
            downloadedEpisodes = await FetchDownloadedEpisodesAsync(ct);
            downloadedMovies = await FetchDownloadedMoviesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Bazarr fetch failed; marking all subtitle origins as Unknown.");
            return await ResetAllToUnknownAsync(ct);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var seriesUpdated = await ApplyEpisodeOriginsAsync(db, downloadedEpisodes, ct);
        var movieUpdated = await ApplyMovieOriginsAsync(db, downloadedMovies, ct);

        await db.SaveChangesAsync(ct);
        return new BazarrSyncResult(BazarrReachable: true, seriesUpdated, movieUpdated);
    }

    private async Task<BazarrSyncResult> ResetAllToUnknownAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var s = await db.SubtitleFiles.ExecuteUpdateAsync(
            b => b.SetProperty(x => x.Origin, SubtitleOrigin.Unknown), ct);
        var m = await db.MovieSubtitleFiles.ExecuteUpdateAsync(
            b => b.SetProperty(x => x.Origin, SubtitleOrigin.Unknown), ct);
        return new BazarrSyncResult(BazarrReachable: false, s, m);
    }

    private async Task<int> ApplyEpisodeOriginsAsync(
        CuratarrDbContext db,
        IReadOnlySet<(int SeriesId, string RelPath)> downloaded,
        CancellationToken ct)
    {
        var series = await db.Series
            .Include(s => s.Episodes).ThenInclude(e => e.Subtitles)
            .ToListAsync(ct);

        var updated = 0;
        foreach (var s in series)
        {
            foreach (var episode in s.Episodes)
            {
                var sourceOriginBySuffix = new Dictionary<string, SubtitleOrigin>(StringComparer.OrdinalIgnoreCase);

                foreach (var sub in episode.Subtitles.Where(x => x.Side == FileSide.Source))
                {
                    var key = (s.SonarrId, NormalizeRel(sub.RelativePath));
                    var origin = downloaded.Contains(key) ? SubtitleOrigin.Downloaded : SubtitleOrigin.Original;
                    if (sub.Origin != origin)
                    {
                        sub.Origin = origin;
                        updated++;
                    }
                    sourceOriginBySuffix[sub.Suffix] = origin;
                }

                foreach (var sub in episode.Subtitles.Where(x => x.Side == FileSide.Destination))
                {
                    var origin = sourceOriginBySuffix.TryGetValue(sub.Suffix, out var inherited)
                        ? inherited
                        : SubtitleOrigin.Unknown;
                    if (sub.Origin != origin)
                    {
                        sub.Origin = origin;
                        updated++;
                    }
                }
            }
        }
        return updated;
    }

    private async Task<int> ApplyMovieOriginsAsync(
        CuratarrDbContext db,
        IReadOnlySet<(int RadarrId, string RelPath)> downloaded,
        CancellationToken ct)
    {
        var movies = await db.Movies
            .Include(m => m.Subtitles)
            .ToListAsync(ct);

        var updated = 0;
        foreach (var m in movies)
        {
            var sourceOriginBySuffix = new Dictionary<string, SubtitleOrigin>(StringComparer.OrdinalIgnoreCase);

            foreach (var sub in m.Subtitles.Where(x => x.Side == FileSide.Source))
            {
                var key = (m.RadarrId, NormalizeRel(sub.RelativePath));
                var origin = downloaded.Contains(key) ? SubtitleOrigin.Downloaded : SubtitleOrigin.Original;
                if (sub.Origin != origin)
                {
                    sub.Origin = origin;
                    updated++;
                }
                sourceOriginBySuffix[sub.Suffix] = origin;
            }

            foreach (var sub in m.Subtitles.Where(x => x.Side == FileSide.Destination))
            {
                var origin = sourceOriginBySuffix.TryGetValue(sub.Suffix, out var inherited)
                    ? inherited
                    : SubtitleOrigin.Unknown;
                if (sub.Origin != origin)
                {
                    sub.Origin = origin;
                    updated++;
                }
            }
        }
        return updated;
    }

    private async Task<IReadOnlySet<(int, string)>> FetchDownloadedEpisodesAsync(CancellationToken ct)
    {
        var seriesList = await client.GetSeriesAsync(ct);
        var folderById = seriesList
            .Where(s => !string.IsNullOrWhiteSpace(s.Path))
            .ToDictionary(s => s.SonarrSeriesId, s => TrimTrailingSlash(s.Path!));

        var history = await client.GetAllEpisodeHistoryAsync(ct);
        var set = new HashSet<(int, string)>();
        foreach (var ev in history)
        {
            if (ev.Action != BazarrDownloadAction) continue;
            if (ev.SonarrSeriesId is not { } sid) continue;
            if (string.IsNullOrWhiteSpace(ev.SubtitlesPath)) continue;
            if (!folderById.TryGetValue(sid, out var folder)) continue;
            var rel = ToRelative(folder, ev.SubtitlesPath!);
            if (rel is null) continue;
            set.Add((sid, NormalizeRel(rel)));
        }
        return set;
    }

    private async Task<IReadOnlySet<(int, string)>> FetchDownloadedMoviesAsync(CancellationToken ct)
    {
        var moviesList = await client.GetMoviesAsync(ct);
        // Bazarr's movie.path is the file path; we want the folder.
        var folderById = moviesList
            .Where(m => !string.IsNullOrWhiteSpace(m.Path))
            .ToDictionary(m => m.RadarrId, m => TrimTrailingSlash(Path.GetDirectoryName(m.Path!) ?? string.Empty));

        var history = await client.GetAllMovieHistoryAsync(ct);
        var set = new HashSet<(int, string)>();
        foreach (var ev in history)
        {
            if (ev.Action != BazarrDownloadAction) continue;
            if (ev.RadarrId is not { } rid) continue;
            if (string.IsNullOrWhiteSpace(ev.SubtitlesPath)) continue;
            if (!folderById.TryGetValue(rid, out var folder) || string.IsNullOrEmpty(folder)) continue;
            var rel = ToRelative(folder, ev.SubtitlesPath!);
            if (rel is null) continue;
            set.Add((rid, NormalizeRel(rel)));
        }
        return set;
    }

    private static string? ToRelative(string folder, string fullPath)
    {
        if (!fullPath.StartsWith(folder, StringComparison.Ordinal)) return null;
        var rel = fullPath[folder.Length..].TrimStart('/', '\\');
        return string.IsNullOrEmpty(rel) ? null : rel;
    }

    private static string TrimTrailingSlash(string p) => p.TrimEnd('/', '\\');

    private static string NormalizeRel(string p) => p.Replace('\\', '/');
}
