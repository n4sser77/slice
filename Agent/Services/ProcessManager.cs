using System.Diagnostics;
using System.Text.Json;
using Agent.Serialization;
using Agent.Services.Exceptions;
using Slice.Common.Models;

namespace Agent.Services;

public partial class ProcessManager
{
  private readonly string _targetDir;
  private readonly IPortManager _portManager;
  private readonly string _systemctlBinary;

  public ProcessManager(string targetDir, IPortManager portManager, string systemctlBinary = "systemctl")
  {
    _targetDir = targetDir;
    _portManager = portManager;
    _systemctlBinary = systemctlBinary;
  }

  public async Task<List<SystemdService>> GetServices()
  {
    var psi = new ProcessStartInfo
    {
      FileName = _systemctlBinary,
      Arguments = "--user list-units --type=service --all --output=json --no-pager slice-*.service",
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    using var process = Process.Start(psi);
    if (process == null)
      throw new SystemctlException("Failed to start systemctl for service discovery.");

    string output = await process.StandardOutput.ReadToEndAsync();
    string error = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
      throw new SystemctlException($"systemctl failed while listing services: {error.Trim()}");

    var services = JsonSerializer.Deserialize(output, AppJsonContext.Default.ListSystemdService) ?? [];

    return
    [
        .. services
                .Where(static s =>
                    !string.IsNullOrWhiteSpace(s.Unit) &&
                    s.Unit.StartsWith("slice-", StringComparison.Ordinal) &&
                    s.Unit.EndsWith(".service", StringComparison.Ordinal))
                .OrderBy(static s => s.Unit, StringComparer.Ordinal)
    ];
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
      FileName = _systemctlBinary,
      Arguments = $"--user is-active {appName}.service",
      UseShellExecute = false,
      CreateNoWindow = true,
    });
    process?.WaitForExit();
    return process?.ExitCode == 0;
  }

  private void RunSystemctlUser(string args) => Process.Start(new ProcessStartInfo
  {
    FileName = _systemctlBinary,
    Arguments = $"--user {args}",
    UseShellExecute = false,
    CreateNoWindow = true,
  })?.WaitForExit();

  public async Task<int> CreateSystemdService(string appName, string dllName)
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
    return port;
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
    var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT")
        ?? throw new InvalidOperationException("DOTNET_ROOT is not set. Ensure dotnet is installed and DOTNET_ROOT is configured.");
    var dotnetExe = Path.Combine(dotnetRoot, "dotnet");
    return
    $"""
        [Unit]
        Description=Uploaded C# Service: {appName}

        [Service]
        WorkingDirectory={appDir}
        ExecStart={dotnetExe} {appDir}/{dllName}.dll
        Restart=always
        NoNewPrivileges=true
        PrivateTmp=true

        Environment=ASPNETCORE_HTTP_PORTS={port}
        Environment=ASPNETCORE_URLS={url}
        Environment=ASPNETCORE_ENVIRONMENT=Production
        Environment=ASPNETCORE_HOSTFILTERING__ALLOWEDHOSTS={domain}
        Environment=DOTNET_ROOT={dotnetRoot}

        [Install]
        WantedBy=default.target
        """;
  }
  public async Task<ServiceStatus?> GetServiceStatusAsync(string serviceName)
  {
    var args = $"show {serviceName}.service --property=Id,Description,LoadState,ActiveState,SubState,StateChangeTimestamp,MainPID,MemoryCurrent,MemoryPeak,CPUUsageNSec,Result";
    var psi = new ProcessStartInfo
    {
      FileName = _systemctlBinary,
      Arguments = $"--user {args}",
      RedirectStandardOutput = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    using var process = Process.Start(psi)!;
    string output = await process.StandardOutput.ReadToEndAsync();
    await process.WaitForExitAsync();

    var status = SystemdOutputParser.ParseServiceStatus(output);
    return status.LoadState == "not-found" ? null : status;
  }

  public async Task<bool> StopServiceAsync(string serviceName)
  {
    var psi = new ProcessStartInfo
    {
      FileName = _systemctlBinary,
      ArgumentList = { "--user", "stop", $"{serviceName}.service" },
      RedirectStandardOutput = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    using var process = Process.Start(psi)!;
    string output = await process.StandardOutput.ReadToEndAsync();
    await process.WaitForExitAsync();

    return process.ExitCode == 0;
  }
}
