using Agent.Services;
using Agent.Services.Exceptions;
namespace Agent.Tests.Services;

public class ProcessManagerTests : IDisposable
{
  private readonly ProcessManager _sut;
  private static readonly string systemdPath =
     Path.Combine(Path.GetTempPath(), "slice-systemd-tests");

  public ProcessManagerTests()
  {
    Directory.CreateDirectory(systemdPath);
    _sut = new ProcessManager(systemdPath, new PortManager());
  }

  public void Dispose()
  {
    if (Directory.Exists(systemdPath))
      Directory.Delete(systemdPath, true);
  }

  [Fact]
  public async Task CreateSystemdService_CreatesServiceFileAsync()
  {
    string appName = "slice-testapp";
    var path = Path.Combine(systemdPath, "slice-testapp.service");

    await _sut.CreateSystemdService(appName, "testapp");

    Assert.True(File.Exists(path));
  }

  [Fact]
  public async Task CreateSystemdService_ContainsSecurityHardening()
  {
    string appName = "slice-testapp";
    var path = Path.Combine(systemdPath, "slice-testapp.service");

    await _sut.CreateSystemdService(appName, "testapp");

    var content = File.ReadAllText(path);
    Assert.Contains("NoNewPrivileges=true", content);
    Assert.Contains("PrivateTmp=true", content);
  }

  [Fact]
  public async Task CreateSystemdService_ServiceRunsTheUploadedDll()
  {
    string appName = "slice-myapp";
    string dllName = "myapp";
    var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? "/fake/dotnet";
    var path = Path.Combine(systemdPath, "slice-myapp.service");

    await _sut.CreateSystemdService(appName, dllName);

    var content = File.ReadAllText(path);
    Assert.Contains($"Description=Uploaded C# Service: {appName}", content);
    Assert.Contains($"ExecStart={dotnetRoot}/dotnet", content);
    Assert.Contains($"{dllName}.dll", content);
    Assert.Contains($"DOTNET_ROOT={dotnetRoot}", content);
  }

  [Fact]
  public async Task CreateSystemdService_ThrowsWhenDotnetRootIsNotSet()
  {
    var previous = Environment.GetEnvironmentVariable("DOTNET_ROOT");
    Environment.SetEnvironmentVariable("DOTNET_ROOT", null);
    try
    {
      await Assert.ThrowsAsync<InvalidOperationException>(
          () => _sut.CreateSystemdService("slice-myapp", "myapp"));
    }
    finally
    {
      Environment.SetEnvironmentVariable("DOTNET_ROOT", previous);
    }
  }

  [Fact]
  public async Task CreateSystemdService_OverwritesExistingServiceFile()
  {
    string appName = "slice-testapp";
    var path = Path.Combine(systemdPath, "slice-testapp.service");

    await _sut.CreateSystemdService(appName, "v1dll");
    await _sut.CreateSystemdService(appName, "v2dll");

    var content = File.ReadAllText(path);
    Assert.Contains("v2dll.dll", content);
    Assert.DoesNotContain("v1dll.dll", content);
  }

  // [Fact]
  // [Trait("Category", "Integration")]
  // public async Task ListServices_GetsAllServicesFromSystemd()
  // {
  //     List<SystemdService> services = await _sut.ListServices();
  //
  //     Assert.True(services.Count > 0);
  //     Console.WriteLine(JsonSerializer.Serialize(services, AppJsonContext.Default.ListSystemdService));
  // }

  [Fact]
  public async Task GetServices_FiltersAndSortsSliceServices()
  {
    var tempDir = Path.Combine(Path.GetTempPath(), $"slice-systemctl-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);

    try
    {
      var scriptPath = Path.Combine(tempDir, "fake-systemctl.sh");
      File.WriteAllText(scriptPath,
          """
                #!/usr/bin/env bash
                cat <<'JSON'
                [
                  {"Unit":"slice-z.service","Loaded":"loaded","Active":"active","Sub":"running","Description":"z service"},
                  {"Unit":"not-slice.service","Loaded":"loaded","Active":"active","Sub":"running","Description":"other service"},
                  {"Unit":"slice-a.service","Loaded":"loaded","Active":"inactive","Sub":"dead","Description":"a service"},
                  {"Unit":"slice-a.socket","Loaded":"loaded","Active":"active","Sub":"listening","Description":"a socket"}
                ]
                JSON
                exit 0
                """);
      MakeExecutable(scriptPath);

      var sut = new ProcessManager(tempDir, new PortManager(), scriptPath);

      var services = await sut.GetServices();

      Assert.Collection(services,
          s => Assert.Equal("slice-a.service", s.Unit),
          s => Assert.Equal("slice-z.service", s.Unit));
    }
    finally
    {
      Directory.Delete(tempDir, true);
    }
  }

  [Fact]
  public async Task GetServices_ThrowsOnSystemctlFailure()
  {
    var tempDir = Path.Combine(Path.GetTempPath(), $"slice-systemctl-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);

    try
    {
      var scriptPath = Path.Combine(tempDir, "fake-systemctl.sh");
      File.WriteAllText(scriptPath,
          """
                #!/usr/bin/env bash
                echo "permission denied" >&2
                exit 1
                """);
      MakeExecutable(scriptPath);

      var sut = new ProcessManager(tempDir, new PortManager(), scriptPath);

      var ex = await Assert.ThrowsAsync<SystemctlException>(sut.GetServices);
      Assert.Contains("permission denied", ex.Message);
    }
    finally
    {
      Directory.Delete(tempDir, true);
    }
  }

  [Fact]
  public async Task GetServices_ReturnsEmptyListWhenNoSliceServices()
  {
    var tempDir = Path.Combine(Path.GetTempPath(), $"slice-systemctl-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);

    try
    {
      var scriptPath = Path.Combine(tempDir, "fake-systemctl.sh");
      File.WriteAllText(scriptPath,
          """
                #!/usr/bin/env bash
                cat <<'JSON'
                [{"Unit":"dbus.service","Loaded":"loaded","Active":"active","Sub":"running","Description":"D-Bus"}]
                JSON
                exit 0
                """);
      MakeExecutable(scriptPath);

      var sut = new ProcessManager(tempDir, new PortManager(), scriptPath);

      var services = await sut.GetServices();
      Assert.Empty(services);
    }
    finally
    {
      Directory.Delete(tempDir, true);
    }
  }

  private static void MakeExecutable(string path)
  {
    if (OperatingSystem.IsWindows())
      return;

    var mode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    File.SetUnixFileMode(path, mode);
  }
}
