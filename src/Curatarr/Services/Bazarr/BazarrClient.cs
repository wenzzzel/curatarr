using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Curatarr.Services.Bazarr;

public class BazarrClient(HttpClient http)
{
    private const int PageSize = 1000;

    public async Task<List<BazarrSeriesDto>> GetSeriesAsync(CancellationToken ct = default)
    {
        var resp = await http.GetFromJsonAsync<BazarrSeriesResponse>("api/series", ct);
        return resp?.Data ?? [];
    }

    public async Task<List<BazarrMovieDto>> GetMoviesAsync(CancellationToken ct = default)
    {
        var resp = await http.GetFromJsonAsync<BazarrMoviesResponse>("api/movies", ct);
        return resp?.Data ?? [];
    }

    public async Task<List<BazarrHistoryEventDto>> GetAllEpisodeHistoryAsync(CancellationToken ct = default) =>
        await PaginateHistoryAsync("api/episodes/history", ct);

    public async Task<List<BazarrHistoryEventDto>> GetAllMovieHistoryAsync(CancellationToken ct = default) =>
        await PaginateHistoryAsync("api/movies/history", ct);

    private async Task<List<BazarrHistoryEventDto>> PaginateHistoryAsync(string path, CancellationToken ct)
    {
        var all = new List<BazarrHistoryEventDto>();
        var start = 0;
        while (true)
        {
            var resp = await http.GetFromJsonAsync<BazarrHistoryResponse>($"{path}?start={start}&length={PageSize}", ct);
            if (resp is null || resp.Data.Count == 0) break;
            all.AddRange(resp.Data);
            if (resp.Data.Count < PageSize) break;
            start += PageSize;
        }
        return all;
    }
}

public record BazarrSeriesResponse(List<BazarrSeriesDto> Data);
public record BazarrMoviesResponse(List<BazarrMovieDto> Data);
public record BazarrHistoryResponse(List<BazarrHistoryEventDto> Data);

public record BazarrSeriesDto(int SonarrSeriesId, string? Path);
public record BazarrMovieDto(int RadarrId, string? Path);

public class BazarrHistoryEventDto
{
    public int Action { get; init; }
    public int? SonarrSeriesId { get; init; }
    public int? SonarrEpisodeId { get; init; }
    public int? RadarrId { get; init; }

    [JsonPropertyName("subtitles_path")]
    public string? SubtitlesPath { get; init; }
}
