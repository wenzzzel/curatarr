using System.Net.Http.Json;

namespace Curatarr.Services.Radarr;

public class RadarrClient(HttpClient http)
{
    public Task<RadarrSystemStatus?> GetSystemStatusAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<RadarrSystemStatus>("api/v3/system/status", ct);

    public async Task<List<RadarrMovieDto>> GetMoviesAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<RadarrMovieDto>>("api/v3/movie", ct) ?? [];
}

public record RadarrSystemStatus(string Version, string AppName, string InstanceName);

public record RadarrMovieDto(
    int Id,
    string Title,
    string? Path,
    bool HasFile,
    RadarrMovieFileDto? MovieFile);

public record RadarrMovieFileDto(int Id, string RelativePath, long Size);
