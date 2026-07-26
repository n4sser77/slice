using System.IO.Compression;
using Agent.Cli;
using Agent.Cli.Commands;

namespace Agent.Cli.Tests;

public class DeployServiceCommandTests
{
  [Fact]
  public void TryCreatePackage_PreservesNestedPublishAssets()
  {
    var publishPath = Path.Combine(
        Path.GetTempPath(),
        $"slice-package-tests-{Guid.NewGuid():N}");

    try
    {
      WriteFile(publishPath, "BlazorApp.dll");
      WriteFile(publishPath, "appsettings.json");
      WriteFile(publishPath, "wwwroot/index.html");
      WriteFile(publishPath, "wwwroot/_framework/blazor.webassembly.js");
      WriteFile(publishPath, "wwwroot/css/app.css", "css");
      WriteFile(publishPath, "wwwroot/js/app.css", "javascript");

      using var httpClient = new HttpClient();
      var config = new CliConfig(new Uri("http://localhost:5165/v1/"), "linux-arm64");
      var sut = new DeployServiceCommand("BlazorApp", false, null, httpClient, config);

      var result = sut.TryCreatePackage(publishPath);

      Assert.Null(result.error);
      Assert.Equal("BlazorApp.zip", result.fileName);
      using var zipStream = Assert.IsType<MemoryStream>(result.zipStream);
      using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
      var entries = archive.Entries.Select(entry => entry.FullName).ToHashSet();

      Assert.Contains("BlazorApp.dll", entries);
      Assert.Contains("appsettings.json", entries);
      Assert.Contains("wwwroot/index.html", entries);
      Assert.Contains("wwwroot/_framework/blazor.webassembly.js", entries);
      Assert.Equal("css", ReadEntry(archive, "wwwroot/css/app.css"));
      Assert.Equal("javascript", ReadEntry(archive, "wwwroot/js/app.css"));
    }
    finally
    {
      if (Directory.Exists(publishPath))
        Directory.Delete(publishPath, true);
    }
  }

  private static void WriteFile(string root, string relativePath, string? content = null)
  {
    var path = Path.Combine(root, relativePath);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, content ?? relativePath);
  }

  private static string ReadEntry(ZipArchive archive, string path)
  {
    using var reader = new StreamReader(archive.GetEntry(path)!.Open());
    return reader.ReadToEnd();
  }
}
