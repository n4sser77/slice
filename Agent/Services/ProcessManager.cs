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

    if (process is null)
      throw new SystemctlException("Failed to start systemctl for service discovery.");

    Task<string> readOutput = process.StandardOutput.ReadToEndAsync();
    Task<string> readError = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    string output = await readOutput;
    string error = await readError;

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

  private async Task RunService(string appName)
  {
    await RunSystemctlUser("daemon-reload");
    bool isActive = await IsServiceActiveAsync(appName);
    await RunSystemctlUser(isActive ? $"restart {appName}.service" : $"enable --now {appName}.service");
  }

  private async Task<bool> IsServiceActiveAsync(string appName)
  {
    using var process = Process.Start(new ProcessStartInfo
    {
      FileName = _systemctlBinary,
      Arguments = $"--user is-active {appName}.service",
      UseShellExecute = false,
      CreateNoWindow = true,
    });
    if (process is null) return false;
    await process.WaitForExitAsync();
    return process.ExitCode == 0;
  }

  public async Task DaemonReloadAsync()
    => await RunSystemctlUser("daemon-reload");

  private async Task RunSystemctlUser(string args)
  {
    using var process = Process.Start(new ProcessStartInfo
    {
      FileName = _systemctlBinary,
      Arguments = $"--user {args}",
      UseShellExecute = false,
      CreateNoWindow = true,
      RedirectStandardError = true,
    }) ?? throw new InvalidOperationException($"Failed to start systemctl with args: {args}");
    string error = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0)
      throw new SystemctlException($"systemctl --user {args} failed: {error.Trim()}");
  }

  public async Task<int> CreateSystemdService(string appName, string dllName, string? allowedHost = null)
  {
    string appDir = Path.GetFullPath(Path.Combine("slice", appName));
    int? nullablePort = _portManager.ReserveNextPort();

    int port = nullablePort is null ?
        throw new OutOfPortsException() :
        (int)nullablePort;

    string serviceContent = ConstructServicefile(appName, dllName, appDir, port, allowedHost);

    var servicePath = Path.Combine(_targetDir, $"{appName}.service");
    Directory.CreateDirectory(_targetDir);
    File.WriteAllText(servicePath, serviceContent);

    await RunService(appName);
    return port;
  }

  private static (string, string) ConstructCustomDomainUrl(string appName, int port)
  {
    var domain = "127.0.0.1";
    var url = $"http://{domain}:{port}";
    return (domain, url);
  }

  private static string ConstructServicefile(string appName, string dllName, string appDir, int port, string? allowedHost = null)
  {
    var (domain, url) = ConstructCustomDomainUrl(appName, port);
    var hostFilter = allowedHost ?? domain;
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
        Environment=ASPNETCORE_HOSTFILTERING__ALLOWEDHOSTS={hostFilter}
        Environment=DOTNET_ROOT={dotnetRoot}

        [Install]
        WantedBy=default.target
        """;
  }
  public async Task<ServiceStatus?> GetServiceStatusAsync(string serviceName)
  {
    var psi = new ProcessStartInfo
    {
      FileName = _systemctlBinary,
      RedirectStandardOutput = true,
      UseShellExecute = false,
      CreateNoWindow = true,
      ArgumentList = { "--user", "show", $"{serviceName}.service", "--property=Id,Description,LoadState,ActiveState,SubState,StateChangeTimestamp,MainPID,MemoryCurrent,MemoryPeak,CPUUsageNSec,Result" }
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

  public async Task DeleteServiceAsync(string serviceName)
  {
    string svc = $"{serviceName}.service";

    await RunSystemctlUser($"stop {svc}");
    await RunSystemctlUser($"disable {svc}");

    var servicePath = Path.Combine(_targetDir, svc);
    if (File.Exists(servicePath))
      File.Delete(servicePath);

    await RunSystemctlUser("daemon-reload");

    var appDir = Path.GetFullPath(Path.Combine("slice", serviceName));
    if (Directory.Exists(appDir))
      Directory.Delete(appDir, recursive: true);
  }

  public int? GetServicePortFromFile(string serviceName)
  {
    var servicePath = Path.Combine(_targetDir, $"{serviceName}.service");
    if (!File.Exists(servicePath))
      return null;

    var lines = File.ReadAllLines(servicePath);
    foreach (var line in lines)
    {
      if (line.StartsWith("Environment=ASPNETCORE_HTTP_PORTS=", StringComparison.Ordinal))
      {
        var val = line["Environment=ASPNETCORE_HTTP_PORTS=".Length..].Trim();
        if (int.TryParse(val, out var port))
          return port;
      }
      if (line.StartsWith("Environment=ASPNETCORE_URLS=", StringComparison.Ordinal))
      {
        var val = line["Environment=ASPNETCORE_URLS=".Length..].Trim();
        var lastColon = val.LastIndexOf(':');
        if (lastColon >= 0 && int.TryParse(val[(lastColon + 1)..], out var port))
          return port;
      }
    }
    return null;
  }
}
