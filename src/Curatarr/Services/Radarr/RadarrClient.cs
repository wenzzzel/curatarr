using System.Net.Http.Json;

namespace Curatarr.Services.Radarr;

public class RadarrClient(HttpClient http)
{
    public Task<RadarrSystemStatus?> GetSystemStatusAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<RadarrSystemStatus>("api/v3/system/status", ct);
}

public record RadarrSystemStatus(string Version, string AppName, string InstanceName);
