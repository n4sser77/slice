using System.Text.Json;
using Agent.Models;
using Agent.Serialization;
using Agent.Services;
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
    public async Task CreateSystemdService_UsesAppNameInServiceFileAsync()
    {
        string appName = "slice-myapp";
        string dllName = "myapp";
        var path = Path.Combine(systemdPath, "slice-myapp.service");

        await _sut.CreateSystemdService(appName, dllName);

        var content = File.ReadAllText(path);
        Assert.Contains("Description=Uploaded C# Service: slice-myapp", content);
        Assert.Contains($"WorkingDirectory=", content);
        Assert.Contains($"ExecStart=/usr/bin/dotnet", content);
        Assert.Contains($"{dllName}.dll", content);
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
}
