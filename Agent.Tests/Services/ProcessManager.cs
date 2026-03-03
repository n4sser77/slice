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
        _sut = new ProcessManager(systemdPath);
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

        await _sut.CreateSystemdService(appName);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task CreateSystemdService_ContainsSecurityHardening()
    {
        string appName = "slice-testapp";
        var path = Path.Combine(systemdPath, "slice-testapp.service");

        await _sut.CreateSystemdService(appName);

        var content = File.ReadAllText(path);
        Assert.Contains("DynamicUser=yes", content);
        Assert.Contains("NoNewPrivileges=true", content);
        Assert.Contains("PrivateTmp=true", content);
    }

    [Fact]
    public async Task CreateSystemdService_UsesAppNameInServiceFileAsync()
    {
        string appName = "slice-myapp";
        var path = Path.Combine(systemdPath, "slice-myapp.service");

        await _sut.CreateSystemdService(appName);

        var content = File.ReadAllText(path);
        Assert.Contains("Description=Uploaded C# Service: slice-myapp", content);
        Assert.Contains("WorkingDirectory=slice/slice-myapp", content);
    }

    [Fact]
    public async Task ListServices_GetsAllServicesFromSystemd()
    {


        List<SystemdService> services = await _sut.ListServices();

        Assert.True(services.Count > 0);
        Console.WriteLine(JsonSerializer.Serialize(services, AppJsonContext.Default.ListSystemdService));
    }
}
