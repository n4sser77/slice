using Agent.Services;
using Agent.Services.Exceptions;
namespace Agent.Tests.Services;

public class ProcessManagerTests : IDisposable
{
  private readonly ProcessManager _sut;
  private readonly string _tempDir =
      Path.Combine(Path.GetTempPath(), $"slice-systemd-tests-{Guid.NewGuid():N}");

  public ProcessManagerTests()
  {
    Directory.CreateDirectory(_tempDir);
    _sut = new ProcessManager(_tempDir, new PortManager(), CreateFakeSystemctl(_tempDir, "exit 0"));
  }

  public void Dispose()
  {
    if (Directory.Exists(_tempDir))
      Directory.Delete(_tempDir, true);
  }

  [Fact]
  public async Task CreateSystemdService_CreatesServiceFileAsync()
  {
    await _sut.CreateSystemdService("slice-testapp", "testapp");

    Assert.True(File.Exists(Path.Combine(_tempDir, "slice-testapp.service")));
  }

  [Fact]
  public async Task CreateSystemdService_ContainsSecurityHardening()
  {
    await _sut.CreateSystemdService("slice-testapp", "testapp");

    var content = File.ReadAllText(Path.Combine(_tempDir, "slice-testapp.service"));
    Assert.Contains("NoNewPrivileges=true", content);
    Assert.Contains("PrivateTmp=true", content);
  }

  [Fact]
  public async Task CreateSystemdService_ServiceRunsTheUploadedDll()
  {
    var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? "/fake/dotnet";

    await _sut.CreateSystemdService("slice-myapp", "myapp");

    var content = File.ReadAllText(Path.Combine(_tempDir, "slice-myapp.service"));
    Assert.Contains("Description=Uploaded C# Service: slice-myapp", content);
    Assert.Contains($"ExecStart={dotnetRoot}/dotnet", content);
    Assert.Contains("myapp.dll", content);
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
    await _sut.CreateSystemdService("slice-testapp", "v1dll");
    await _sut.CreateSystemdService("slice-testapp", "v2dll");

    var content = File.ReadAllText(Path.Combine(_tempDir, "slice-testapp.service"));
    Assert.Contains("v2dll.dll", content);
    Assert.DoesNotContain("v1dll.dll", content);
  }

  [Fact]
  public async Task GetServices_FiltersAndSortsSliceServices()
  {
    var script = """
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
        """;

    var services = await WithFakeSystemctl(script, sut => sut.GetServices());

    Assert.Collection(services,
        s => Assert.Equal("slice-a.service", s.Unit),
        s => Assert.Equal("slice-z.service", s.Unit));
  }

  [Fact]
  public async Task GetServices_ThrowsOnSystemctlFailure()
  {
    var script = """
        #!/usr/bin/env bash
        echo "permission denied" >&2
        exit 1
        """;

    var ex = await Assert.ThrowsAsync<SystemctlException>(
        () => WithFakeSystemctl(script, sut => sut.GetServices()));

    Assert.Contains("permission denied", ex.Message);
  }

  [Fact]
  public async Task GetServices_ReturnsEmptyListWhenNoSliceServices()
  {
    var script = """
        #!/usr/bin/env bash
        cat <<'JSON'
        [{"Unit":"dbus.service","Loaded":"loaded","Active":"active","Sub":"running","Description":"D-Bus"}]
        JSON
        exit 0
        """;

    var services = await WithFakeSystemctl(script, sut => sut.GetServices());

    Assert.Empty(services);
  }

  private static async Task<T> WithFakeSystemctl<T>(string script, Func<ProcessManager, Task<T>> test)
  {
    var tempDir = Path.Combine(Path.GetTempPath(), $"slice-systemctl-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);
    try
    {
      var sut = new ProcessManager(tempDir, new PortManager(), CreateFakeSystemctl(tempDir, script));
      return await test(sut);
    }
    finally
    {
      Directory.Delete(tempDir, true);
    }
  }

  private static string CreateFakeSystemctl(string dir, string script)
  {
    var path = Path.Combine(dir, "fake-systemctl.sh");
    File.WriteAllText(path, $"#!/usr/bin/env bash\n{script}\n");
    MakeExecutable(path);
    return path;
  }

  private static void MakeExecutable(string path)
  {
    if (OperatingSystem.IsWindows()) return;
    File.SetUnixFileMode(path,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
  }
}
