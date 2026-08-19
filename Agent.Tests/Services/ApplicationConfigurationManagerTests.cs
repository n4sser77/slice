using Agent.Configuration;
using Agent.Services;
using Slice.Common.Models;

namespace Agent.Tests.Services;

public sealed class ApplicationConfigurationManagerTests : IDisposable
{
  private const string ApplicationName = "slice-demo";
  private readonly string _directory = Path.Combine(
      Path.GetTempPath(),
      $"slice-config-manager-{Guid.NewGuid():N}");
  private readonly ApplicationDataPaths _paths;
  private readonly ApplicationConfigurationStore _store;
  private readonly ApplicationConfigurationManager _manager;

  public ApplicationConfigurationManagerTests()
  {
    _paths = new ApplicationDataPaths(_directory);
    _store = new ApplicationConfigurationStore(_paths);
    _manager = new ApplicationConfigurationManager(
        _store,
        new SystemdEnvironmentMaterializer(_paths));
  }

  public void Dispose()
  {
    if (Directory.Exists(_directory))
      Directory.Delete(_directory, recursive: true);
  }

  [Fact]
  public async Task PrepareRuntime_NoInputPreservesStoredConfiguration()
  {
    var configuration = Configuration(("KEY", "preserved"));
    await _manager.PrepareRuntimeAsync(ApplicationName, configuration);

    var path = await _manager.PrepareRuntimeAsync(ApplicationName, requestedConfiguration: null);

    Assert.Contains("KEY=\"preserved\"", await File.ReadAllTextAsync(path!));
  }

  [Fact]
  public async Task PrepareRuntime_ExplicitEmptyConfigurationClearsRuntimeValues()
  {
    await _manager.PrepareRuntimeAsync(ApplicationName, Configuration(("KEY", "old")));

    var path = await _manager.PrepareRuntimeAsync(ApplicationName, ApplicationConfiguration.Empty);

    Assert.Null(path);
    Assert.False(File.Exists(_paths.GetSystemdEnvironmentFile(ApplicationName)));
    Assert.Empty((await _store.LoadAsync(ApplicationName)).Values);
  }

  [Fact]
  public async Task Delete_RemovesAllApplicationConfiguration()
  {
    await _manager.PrepareRuntimeAsync(ApplicationName, Configuration(("KEY", "value")));

    await _manager.DeleteAsync(ApplicationName);

    Assert.False(Directory.Exists(_paths.GetApplicationDirectory(ApplicationName)));
  }

  private static ApplicationConfiguration Configuration(params (string Key, string Value)[] entries) =>
      new(ApplicationConfiguration.CurrentSchemaVersion, entries.ToDictionary());
}
