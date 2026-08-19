using Agent.Configuration;
using Agent.Services;
using Slice.Common.Models;

namespace Agent.Tests.Services;

public sealed class ApplicationConfigurationStoreTests : IDisposable
{
  private readonly string _directory = Path.Combine(
      Path.GetTempPath(),
      $"slice-config-store-{Guid.NewGuid():N}");
  private readonly ApplicationDataPaths _paths;
  private readonly ApplicationConfigurationStore _store;

  public ApplicationConfigurationStoreTests()
  {
    _paths = new ApplicationDataPaths(_directory);
    _store = new ApplicationConfigurationStore(_paths);
  }

  public void Dispose()
  {
    if (Directory.Exists(_directory))
      Directory.Delete(_directory, recursive: true);
  }

  [Fact]
  public async Task ReplaceAndLoad_RoundTripsCanonicalConfiguration()
  {
    var expected = Configuration(("ConnectionStrings:Postgres", "Host=db;Password=secret"));

    await _store.ReplaceAsync("slice-demo", expected);
    var actual = await _store.LoadAsync("slice-demo");

    Assert.Equal(expected.Values, actual.Values);
    Assert.Equal(ApplicationConfiguration.CurrentSchemaVersion, actual.SchemaVersion);
    var storedJson = await File.ReadAllTextAsync(_paths.GetConfigurationFile("slice-demo"));
    Assert.Contains("\"schemaVersion\"", storedJson);
    Assert.Contains("\"values\"", storedJson);
  }

  [Fact]
  public async Task Replace_EmptyConfigurationPersistsAnExplicitEmptySnapshot()
  {
    await _store.ReplaceAsync("slice-demo", ApplicationConfiguration.Empty);

    Assert.True(File.Exists(_paths.GetConfigurationFile("slice-demo")));
    Assert.Empty((await _store.LoadAsync("slice-demo")).Values);
  }

  [Fact]
  public async Task Store_UsesPrivateUnixPermissions()
  {
    if (OperatingSystem.IsWindows())
      return;

    await _store.ReplaceAsync("slice-demo", Configuration(("KEY", "value")));

    Assert.Equal(
        UnixFileMode.UserRead | UnixFileMode.UserWrite,
        File.GetUnixFileMode(_paths.GetConfigurationFile("slice-demo")));
    Assert.Equal(
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
        File.GetUnixFileMode(_paths.GetApplicationDirectory("slice-demo")));
  }

  [Fact]
  public async Task Delete_RemovesCanonicalAndRuntimeFiles()
  {
    await _store.ReplaceAsync("slice-demo", Configuration(("KEY", "value")));

    await _store.DeleteAsync("slice-demo");

    Assert.False(Directory.Exists(_paths.GetApplicationDirectory("slice-demo")));
  }

  private static ApplicationConfiguration Configuration(params (string Key, string Value)[] entries) =>
      new(ApplicationConfiguration.CurrentSchemaVersion, entries.ToDictionary());
}
