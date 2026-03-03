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
            Arguments = "list-units --type=service --all --output=json --no-pager",
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
    private async Task RunService(string appName)
    {
        string[] commands = ["daemon-reload", $"enable --now {appName}.service"];

        foreach (var cmd in commands)
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = "systemctl",
                Arguments = cmd,
                UseShellExecute = false,
                CreateNoWindow = true,

            })?.WaitForExit();
        }
    }
    public async Task CreateSystemdService(string appName)
    {
        string appDir = Path.Combine("slice", appName);
        string serviceContent =
            $@"
            [Unit]
            Description=Uploaded C# Service: {appName}

            [Service]
            WorkingDirectory={appDir}
            ExecStart=/usr/bin/dotnet {appDir}/{appName}.dll
            Restart=always
            DynamicUser=yes
            NoNewPrivileges=true
            PrivateTmp=true

            [Install]
            WantedBy=multi-user.target";

        var servicePath = Path.Combine(_targetDir, $"{appName}.service");
        File.WriteAllText(servicePath, serviceContent.Trim());

        await RunService(appName);
    }
}
