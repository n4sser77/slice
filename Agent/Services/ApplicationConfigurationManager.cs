using Agent.Services.Interfaces;
using Slice.Common.Models;

namespace Agent.Services;

public sealed class ApplicationConfigurationManager(
    IApplicationConfigurationStore store,
    IRuntimeConfigurationMaterializer materializer)
{
  public async Task<string?> PrepareRuntimeAsync(
      string applicationName,
      ApplicationConfiguration? requestedConfiguration,
      CancellationToken cancellationToken = default)
  {
    ApplicationConfiguration effectiveConfiguration;
    if (requestedConfiguration is null)
    {
      effectiveConfiguration = await store.LoadAsync(applicationName, cancellationToken);
    }
    else
    {
      await store.ReplaceAsync(applicationName, requestedConfiguration, cancellationToken);
      effectiveConfiguration = requestedConfiguration;
    }

    return await materializer.MaterializeAsync(
        applicationName,
        effectiveConfiguration,
        cancellationToken);
  }

  public async Task DeleteAsync(string applicationName)
  {
    await materializer.DeleteAsync(applicationName);
    await store.DeleteAsync(applicationName);
  }
}
