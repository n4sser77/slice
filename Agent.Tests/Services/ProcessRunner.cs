using Agent.Services;
namespace Agent.Tests;

public class ProcessRunnerTests : IDisposable
{
    private readonly ProcessRunner _sut;
    private static readonly string systemdPath =
       Path.Combine(Path.GetTempPath(), "slice-systemd-tests");

    public ProcessRunnerTests()
    {
        Directory.CreateDirectory(systemdPath);
        _sut = new ProcessRunner(systemdPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(systemdPath))
            Directory.Delete(systemdPath, true);
    }

    [Fact]
    public void CreateSystemdService_CreatesServiceFile()
    {
        string appName = "slice-testapp";
        var path = Path.Combine(systemdPath, "slice-testapp.service");

        _sut.CreateSystemdService(appName);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void CreateSystemdService_ContainsSecurityHardening()
    {
        string appName = "slice-testapp";
        var path = Path.Combine(systemdPath, "slice-testapp.service");

        _sut.CreateSystemdService(appName);

        var content = File.ReadAllText(path);
        Assert.Contains("DynamicUser=yes", content);
        Assert.Contains("NoNewPrivileges=true", content);
        Assert.Contains("PrivateTmp=true", content);
    }

    [Fact]
    public void CreateSystemdService_UsesAppNameInServiceFile()
    {
        string appName = "slice-myapp";
        var path = Path.Combine(systemdPath, "slice-myapp.service");

        _sut.CreateSystemdService(appName);

        var content = File.ReadAllText(path);
        Assert.Contains("Description=Uploaded C# Service: slice-myapp", content);
        Assert.Contains("WorkingDirectory=slice/slice-myapp", content);
    }
}
