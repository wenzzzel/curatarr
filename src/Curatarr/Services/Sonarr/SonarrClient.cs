using System.Net.Http.Json;

namespace Curatarr.Services.Sonarr;

public class SonarrClient(HttpClient http)
{
    public Task<SonarrSystemStatus?> GetSystemStatusAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<SonarrSystemStatus>("api/v3/system/status", ct);

    public async Task<List<SonarrSeriesDto>> GetSeriesAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<SonarrSeriesDto>>("api/v3/series", ct) ?? [];

    public async Task<List<SonarrEpisodeDto>> GetEpisodesAsync(int seriesId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<SonarrEpisodeDto>>($"api/v3/episode?seriesId={seriesId}", ct) ?? [];

    public async Task<List<SonarrEpisodeFileDto>> GetEpisodeFilesAsync(int seriesId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<SonarrEpisodeFileDto>>($"api/v3/episodefile?seriesId={seriesId}", ct) ?? [];
}

public record SonarrSystemStatus(string Version, string AppName, string InstanceName);

public record SonarrSeriesDto(int Id, string Title, string? Path);

public record SonarrEpisodeDto(
    int Id,
    int SeasonNumber,
    int EpisodeNumber,
    string Title,
    string? AirDateUtc,
    bool HasFile,
    int? EpisodeFileId);

public record SonarrEpisodeFileDto(int Id, string RelativePath, long Size);
