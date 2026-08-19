using System.Text;
using Agent.Configuration;
using Agent.Services.Interfaces;
using Slice.Common.Models;

namespace Agent.Services;

public sealed class SystemdEnvironmentMaterializer(ApplicationDataPaths paths)
    : IRuntimeConfigurationMaterializer
{
  public async Task<string?> MaterializeAsync(
      string applicationName,
      ApplicationConfiguration configuration,
      CancellationToken cancellationToken = default)
  {
    var destination = paths.GetSystemdEnvironmentFile(applicationName);
    if (configuration.Values.Count == 0)
    {
      if (File.Exists(destination))
        File.Delete(destination);
      return null;
    }

    paths.CreateRuntimeDirectory(applicationName);

    var content = new StringBuilder();
    foreach (var entry in configuration.Values.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
    {
      content.Append(entry.Key.Replace(":", "__", StringComparison.Ordinal));
      content.Append("=\"");
      AppendEscapedValue(content, entry.Value);
      content.AppendLine("\"");
    }

    await PrivateAtomicFile.WriteTextAsync(destination, content.ToString(), cancellationToken);
    return destination;
  }

  public Task DeleteAsync(string applicationName)
  {
    var path = paths.GetSystemdEnvironmentFile(applicationName);
    if (File.Exists(path))
      File.Delete(path);
    return Task.CompletedTask;
  }

  private static void AppendEscapedValue(StringBuilder output, string value)
  {
    foreach (var character in value)
    {
      var requiresEscaping = character is '\\' or '"' or '$' or '`';
      if (requiresEscaping)
        output.Append('\\');
      output.Append(character);
    }
  }
}
