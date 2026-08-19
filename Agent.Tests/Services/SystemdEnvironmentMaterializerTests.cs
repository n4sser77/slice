using System.Text;
using Agent.Configuration;
using Agent.Services;
using Slice.Common.Models;

namespace Agent.Tests.Services;

public sealed class SystemdEnvironmentMaterializerTests : IDisposable
{
  private readonly string _directory = Path.Combine(
      Path.GetTempPath(),
      $"slice-systemd-environment-{Guid.NewGuid():N}");
  private readonly ApplicationDataPaths _paths;
  private readonly SystemdEnvironmentMaterializer _materializer;

  public SystemdEnvironmentMaterializerTests()
  {
    _paths = new ApplicationDataPaths(_directory);
    _materializer = new SystemdEnvironmentMaterializer(_paths);
  }

  public void Dispose()
  {
    if (Directory.Exists(_directory))
      Directory.Delete(_directory, recursive: true);
  }

  [Fact]
  public async Task Materialize_ConvertsHierarchyAndEscapesSystemdValues()
  {
    var configuration = new ApplicationConfiguration(
        ApplicationConfiguration.CurrentSchemaVersion,
        new Dictionary<string, string>
        {
          ["ConnectionStrings:Postgres"] = "Host=db;Password=$e\"c\\ret`"
        });

    var path = await _materializer.MaterializeAsync("slice-demo", configuration);

    Assert.Equal(_paths.GetSystemdEnvironmentFile("slice-demo"), path);
    Assert.Equal(
        "ConnectionStrings__Postgres=\"Host=db;Password=\\$e\\\"c\\\\ret\\`\"\n",
        await File.ReadAllTextAsync(path!));
    Assert.False((await File.ReadAllBytesAsync(path!)).AsSpan().StartsWith(Encoding.UTF8.Preamble));
  }

  [Fact]
  public async Task Materialize_EmptyConfigurationRemovesRuntimeFile()
  {
    await _materializer.MaterializeAsync("slice-demo", new(
        ApplicationConfiguration.CurrentSchemaVersion,
        new Dictionary<string, string> { ["KEY"] = "value" }));

    var result = await _materializer.MaterializeAsync("slice-demo", ApplicationConfiguration.Empty);

    Assert.Null(result);
    Assert.False(File.Exists(_paths.GetSystemdEnvironmentFile("slice-demo")));
  }
}
