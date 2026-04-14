using System.Diagnostics;
using System.Text.Json;
using Agent.Models;
using Agent.Serialization;

namespace Agent.Services;

public class ProcessManager
{
    private readonly string _targetDir;
    private readonly IPortManager _portManager;

    public ProcessManager(string targetDir, IPortManager portManager)
    {
        _targetDir = targetDir;
        _portManager = portManager;
    }
    public async Task<List<SystemdService>> GetServices()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "systemctl",
            Arguments = "--user list-units \"slice*\" --type=service --all --output=json --no-pager",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return [];
        string output = await process.StandardOutput.ReadToEndAsync();

        List<SystemdService> services = JsonSerializer.Deserialize(output, AppJsonContext.Default.ListSystemdService)
            ?? [];

        return [.. services.Where(s => s.Unit.StartsWith("slice-", StringComparison.OrdinalIgnoreCase))];

    }
    private Task RunService(string appName)
    {
        RunSystemctlUser("daemon-reload");

        bool isActive = IsServiceActive(appName);
        RunSystemctlUser(isActive ? $"restart {appName}.service" : $"enable --now {appName}.service");

        return Task.CompletedTask;
    }

    private bool IsServiceActive(string appName)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "systemctl",
            Arguments = $"--user is-active {appName}.service",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        process?.WaitForExit();
        return process?.ExitCode == 0;
    }

    private void RunSystemctlUser(string args) => Process.Start(new ProcessStartInfo
    {
        FileName = "systemctl",
        Arguments = $"--user {args}",
        UseShellExecute = false,
        CreateNoWindow = true,
    })?.WaitForExit();

    public async Task CreateSystemdService(string appName, string dllName)
    {
        string appDir = Path.GetFullPath(Path.Combine("slice", appName));
        int? nullablePort = _portManager.ReserveNextPort();

        int port = nullablePort is null ?
            throw new OutOfPortsException() :
            (int)nullablePort;

        string serviceContent = ConstructServicefile(appName, dllName, appDir, port);

        var servicePath = Path.Combine(_targetDir, $"{appName}.service");
        Directory.CreateDirectory(_targetDir);
        File.WriteAllText(servicePath, serviceContent);

        await RunService(appName);
    }
    private (string, string) ConstructCustomDomainUrl(string appName, int port)
    {
        var domain = appName.ToString() + ".localhost";
        var url = $"http://{domain}:{port}";
        return (domain, url);
    }


    private string ConstructServicefile(string appName, string dllName, string appDir, int port)
    {

        var (domain, url) = ConstructCustomDomainUrl(appName, port);
        return
        $"""
        [Unit]
        Description=Uploaded C# Service: {appName}
        Environment=ASPNETCORE_HTTP_PORTS={port}
        [Service]
        WorkingDirectory={appDir}
        ExecStart=/usr/bin/dotnet {appDir}/{dllName}.dll
        Restart=always
        NoNewPrivileges=true
        PrivateTmp=true

        Environment=ASPNETCORE_URLS={url}
        Environment=ASPNETCORE_ENVIRONMENT=Production
        Environment=ASPNETCORE_HOSTFILTERING__ALLOWEDHOSTS={domain}

        [Install]
        WantedBy=multi-user.target
        """;
    }

    public class OutOfPortsException : Exception
    {
        public OutOfPortsException() : base("No available ports left in the allocated range (5001-5050).") { }
        public OutOfPortsException(string message) : base(message) { }
        public OutOfPortsException(string message, Exception inner) : base(message, inner) { }
    }
}
