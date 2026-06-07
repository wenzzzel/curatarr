using System.Net.Http.Json;

namespace Curatarr.Services.Sonarr;

public class SonarrClient(HttpClient http)
{
    public Task<SonarrSystemStatus?> GetSystemStatusAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<SonarrSystemStatus>("api/v3/system/status", ct);

    public async Task<List<SonarrSeriesDto>> GetSeriesAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<SonarrSeriesDto>>("api/v3/series", ct) ?? [];
}

public record SonarrSystemStatus(string Version, string AppName, string InstanceName);

public record SonarrSeriesDto(int Id, string Title, string? Path);
