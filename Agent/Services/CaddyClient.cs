using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Agent.Serialization;
using Agent.Services.Interfaces;

namespace Agent.Services;

public class CaddyClient(HttpClient http) : IReverseProxyClient
{
    public async Task RegisterRouteAsync(string appName, string domain, int port, CancellationToken ct = default)
    {
        var route = new CaddyRoute(
            $"slice-{appName}",
            [new CaddyMatch([domain])],
            [new CaddyHandle("reverse_proxy", [new CaddyUpstream($"localhost:{port}")])]
        );

        using var content = JsonContent.Create(route, AppJsonContext.Default.CaddyRoute);
        var response = await http.PostAsync("/config/apps/http/servers/srv0/routes", content, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveRouteAsync(string appName, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/id/slice-{appName}", ct);
        response.EnsureSuccessStatusCode();
    }
}

public record CaddyRoute(
    [property: JsonPropertyName("@id")] string Id,
    [property: JsonPropertyName("match")] CaddyMatch[] Match,
    [property: JsonPropertyName("handle")] CaddyHandle[] Handle
);

public record CaddyMatch(
    [property: JsonPropertyName("host")] string[] Host
);

public record CaddyHandle(
    [property: JsonPropertyName("handler")] string Handler,
    [property: JsonPropertyName("upstreams")] CaddyUpstream[] Upstreams
);

public record CaddyUpstream(
    [property: JsonPropertyName("dial")] string Dial
);
