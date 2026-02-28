namespace Agent.Services;

public class PortManager(int min = 5000, int max = 6000)
{
    private readonly int _minPort = min;
    private readonly int _maxPort = max;
    private readonly HashSet<int> _usedPorts = [];

    public int? ReserveNextPort()
    {
        for (int p = _minPort; p <= _maxPort; p++)
        {
            if (!_usedPorts.Contains(p) && IsPortActuallyFree(p))
            {
                _usedPorts.Add(p);
                return p;
            }
        }
        return null;
    }

    private static bool IsPortActuallyFree(int port)
    {
        // Dubbelkollar mot OS om någon process (utanför din PaaS) tagit porten
        return System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners().All(l => l.Port != port);
    }

    public void ReleasePort(int port) => _usedPorts.Remove(port);
}
