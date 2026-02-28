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
        _sut = new(systemdPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(systemdPath))
            Directory.Delete(systemdPath, true);
    }

    [Fact]
    public void CreateSystemdService_CreatesServiceFile()
    {
        string appName = "slice-testApp";
        var path = Path.Combine(systemdPath, "slice-testApp.service");

        _sut.CreateSystemdService(appName);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void CreateSystemdService_AddsSlicePrefix()
    {
        string appName = "slice-myapp";
        var path = Path.Combine(systemdPath, "slice-myapp.service");

        _sut.CreateSystemdService(appName);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void CreateSystemdService_SanitizesSpecialCharacters()
    {
        string appName = "slice-myapp../../../etc/passwd";
        string safeName = "slice-myappetcpasswd";
        var path = Path.Combine(systemdPath, $"{safeName}.service");

        Directory.CreateDirectory(systemdPath);
        _sut.CreateSystemdService(appName);

        Assert.True(File.Exists(path), $"Expected {path}, files: {string.Join(", ", Directory.GetFiles(systemdPath))}");
        var content = File.ReadAllText(path);
        Assert.DoesNotContain("../../../etc/passwd", content);
    }

    [Fact]
    public void CreateSystemdService_SanitizesSpacesAndSymbols()
    {
        string appName = "slice-my app!@#$%";
        var path = Path.Combine(systemdPath, "slice-myapp.service");

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
}
