using System.Text.Json;
using Agent.Configuration;
using Agent.Serialization;
using Agent.Services.Interfaces;
using Slice.Common.Models;

namespace Agent.Services;

public sealed class ApplicationConfigurationStore(ApplicationDataPaths paths) : IApplicationConfigurationStore
{
  public async Task<ApplicationConfiguration> LoadAsync(
      string applicationName,
      CancellationToken cancellationToken = default)
  {
    var path = paths.GetConfigurationFile(applicationName);
    if (!File.Exists(path))
      return ApplicationConfiguration.Empty;

    await using var stream = File.OpenRead(path);
    var configuration = await JsonSerializer.DeserializeAsync(
        stream,
        AppJsonContext.Default.ApplicationConfiguration,
        cancellationToken);
    if (configuration is null)
      throw new InvalidDataException($"Stored configuration for '{applicationName}' is empty.");

    var error = ApplicationConfigurationValidator.Validate(configuration);
    if (error is not null)
      throw new InvalidDataException($"Stored configuration for '{applicationName}' is invalid: {error}");

    return configuration;
  }

  public async Task ReplaceAsync(
      string applicationName,
      ApplicationConfiguration configuration,
      CancellationToken cancellationToken = default)
  {
    var error = ApplicationConfigurationValidator.Validate(configuration);
    if (error is not null)
      throw new ArgumentException(error, nameof(configuration));

    var destination = paths.GetConfigurationFile(applicationName);
    paths.CreateApplicationDirectory(applicationName);
    await PrivateAtomicFile.WriteAsync(
        destination,
        (stream, token) => JsonSerializer.SerializeAsync(
            stream,
            configuration,
            AppJsonContext.Default.ApplicationConfiguration,
            token),
        cancellationToken);
  }

  public Task DeleteAsync(string applicationName)
  {
    var directory = paths.GetApplicationDirectory(applicationName);
    if (Directory.Exists(directory))
      Directory.Delete(directory, recursive: true);
    return Task.CompletedTask;
  }
}
