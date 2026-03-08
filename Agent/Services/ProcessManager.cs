using System.Diagnostics;
using System.Text.Json;
using Agent.Models;
using Agent.Serialization;

namespace Agent.Services;

public class ProcessManager
{
    private readonly string _targetDir;

    public ProcessManager(string targetDir)
    {
        _targetDir = targetDir;
    }
    public async Task<List<SystemdService>> ListServices()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "systemctl",
            Arguments = "--user list-units --type=service --all --output=json --no-pager",
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

    private void RunSystemctlUser(string args)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "systemctl",
            Arguments = $"--user {args}",
            UseShellExecute = false,
            CreateNoWindow = true,
        })?.WaitForExit();
    }
    public async Task CreateSystemdService(string appName, string dllName)
    {
        string appDir = Path.GetFullPath(Path.Combine("slice", appName));
        string serviceContent = $"""
[Unit]
Description=Uploaded C# Service: {appName}

[Service]
WorkingDirectory={appDir}
ExecStart=/usr/bin/dotnet {appDir}/{dllName}.dll
Restart=always
NoNewPrivileges=true
PrivateTmp=true

[Install]
WantedBy=multi-user.target
""";

        var servicePath = Path.Combine(_targetDir, $"{appName}.service");
        Directory.CreateDirectory(_targetDir);
        File.WriteAllText(servicePath, serviceContent);

        await RunService(appName);
    }
}
