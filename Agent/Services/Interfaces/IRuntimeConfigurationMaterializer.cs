using Slice.Common.Models;

namespace Agent.Services.Interfaces;

public interface IRuntimeConfigurationMaterializer
{
  Task<string?> MaterializeAsync(
      string applicationName,
      ApplicationConfiguration configuration,
      CancellationToken cancellationToken = default);

  Task DeleteAsync(string applicationName);
}
