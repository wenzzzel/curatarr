using Curatarr.Data;
using Curatarr.Models;
using Microsoft.EntityFrameworkCore;

namespace Curatarr.Services.Sonarr;

public record SonarrSyncResult(int SeriesCount, int EpisodeCount);

public class SonarrSyncService(
    SonarrClient client,
    IDbContextFactory<CuratarrDbContext> dbFactory)
{
    public async Task<SonarrSyncResult> SyncAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var seriesCount = await SyncSeriesAsync(db, now, ct);

        var allSeries = await db.Series
            .Include(s => s.Episodes)
            .ThenInclude(e => e.Files)
            .ToListAsync(ct);

        var episodeCount = 0;
        foreach (var series in allSeries)
        {
            episodeCount += await SyncEpisodesForSeriesAsync(db, series, now, ct);
        }

        await db.SaveChangesAsync(ct);
        return new SonarrSyncResult(seriesCount, episodeCount);
    }

    private async Task<int> SyncSeriesAsync(CuratarrDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var incoming = await client.GetSeriesAsync(ct);
        var existing = await db.Series.ToDictionaryAsync(s => s.SonarrId, ct);

        foreach (var dto in incoming)
        {
            if (existing.TryGetValue(dto.Id, out var series))
            {
                series.Title = dto.Title;
                series.Path = dto.Path;
                series.LastSyncedAt = now;
            }
            else
            {
                db.Series.Add(new Series
                {
                    SonarrId = dto.Id,
                    Title = dto.Title,
                    Path = dto.Path,
                    LastSyncedAt = now,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return incoming.Count;
    }

    private async Task<int> SyncEpisodesForSeriesAsync(
        CuratarrDbContext db,
        Series series,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var sonarrEpisodes = await client.GetEpisodesAsync(series.SonarrId, ct);
        var sonarrFiles = await client.GetEpisodeFilesAsync(series.SonarrId, ct);
        var filesById = sonarrFiles.ToDictionary(f => f.Id);
        var existing = series.Episodes.ToDictionary(e => e.SonarrId);

        foreach (var dto in sonarrEpisodes)
        {
            if (!existing.TryGetValue(dto.Id, out var episode))
            {
                episode = new Episode
                {
                    SonarrId = dto.Id,
                    SeriesId = series.Id,
                    SeasonNumber = dto.SeasonNumber,
                    EpisodeNumber = dto.EpisodeNumber,
                    Title = dto.Title,
                    AirDate = ParseAirDate(dto.AirDateUtc),
                    LastSyncedAt = now,
                };
                series.Episodes.Add(episode);
                db.Episodes.Add(episode);
            }
            else
            {
                episode.SeasonNumber = dto.SeasonNumber;
                episode.EpisodeNumber = dto.EpisodeNumber;
                episode.Title = dto.Title;
                episode.AirDate = ParseAirDate(dto.AirDateUtc);
                episode.LastSyncedAt = now;
            }

            if (dto.HasFile && dto.EpisodeFileId is { } fileId && filesById.TryGetValue(fileId, out var file))
            {
                UpsertFile(episode, FileSide.Source, file.RelativePath, file.Size, now);
            }
            else
            {
                RemoveFile(episode, FileSide.Source);
            }
        }

        return sonarrEpisodes.Count;
    }

    private static DateTimeOffset? ParseAirDate(string? airDateUtc) =>
        DateTimeOffset.TryParse(airDateUtc, out var parsed) ? parsed : null;

    private static void UpsertFile(Episode episode, FileSide side, string relativePath, long sizeBytes, DateTimeOffset now)
    {
        var existing = episode.Files.FirstOrDefault(f => f.Side == side);
        if (existing is null)
        {
            episode.Files.Add(new EpisodeFile
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

    private static void RemoveFile(Episode episode, FileSide side)
    {
        var existing = episode.Files.FirstOrDefault(f => f.Side == side);
        if (existing is not null)
        {
            episode.Files.Remove(existing);
        }
    }
}
