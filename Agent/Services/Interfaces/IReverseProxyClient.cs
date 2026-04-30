namespace Agent.Services.Interfaces;

public interface IReverseProxyClient
{
    Task RegisterRouteAsync(string appName, string domain, int port, CancellationToken ct = default);
    Task RemoveRouteAsync(string appName, CancellationToken ct = default);
}
