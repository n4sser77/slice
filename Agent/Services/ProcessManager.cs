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
  public async Task<ServiceStatus> GetServiceStatusAsync(string serviceName)
  {
    var args = $"show {serviceName}.service --property=id,description,loadstate,activestate,substate,statechangetimestamp,mainpid,memorycurrent,memorypeak,cpuusagensec,result";
    var psi = new ProcessStartInfo()
    {
      FileName = _systemctlBinary,
      Arguments = $"--user {args}",
      UseShellExecute = false,
      CreateNoWindow = true
    };

    var output = new System.Text.StringBuilder();
    using var process = new Process() { StartInfo = psi };
    process.OutputDataReceived += (s, e) =>
    {
      if (e.Data is { } l) output.AppendLine(l);
    };
    process.ErrorDataReceived += (s, e) =>
    {
      if (e.Data is { } l) output.AppendLine(l);
    };
    process.Start();
    process.BeginErrorReadLine();
    process.BeginOutputReadLine();

    await process.WaitForExitAsync();
    var result = output.ToString();

    return ParseServiceStatus(result);



  }
  private ServiceStatus ParseServiceStatus(string systemctlOutput)
  {
    var lines = systemctlOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    var dict = lines.Select(line => line.Split('=', 2))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());

    return new ServiceStatus
    {
      Id = dict.GetValueOrDefault("Id") ?? "",
      Description = dict.GetValueOrDefault("Description") ?? "",
      LoadState = dict.GetValueOrDefault("LoadState") ?? "",
      ActiveState = dict.GetValueOrDefault("ActiveState") ?? "",
      SubState = dict.GetValueOrDefault("SubState") ?? "",
      StateChangeTimestamp = dict.GetValueOrDefault("StateChangeTimestamp") ?? "",
      MainPid = int.TryParse(dict.GetValueOrDefault("MainPid"), out int pid) ? pid : 0,
      MemoryCurrent = ulong.TryParse(dict.GetValueOrDefault("MemoryCurrent"), out ulong memCur) ? memCur : 0,
      MemoryPeak = ulong.TryParse(dict.GetValueOrDefault("MemoryPeak"), out ulong memPeak) ? memPeak : 0,
      CpuUsageInSec = ulong.TryParse(dict.GetValueOrDefault("CpuUsageNsec"), out ulong cpuInSec) ? cpuInSec : 0,
      Result = dict.GetValueOrDefault("Result") ?? ""
    };
  }
}
