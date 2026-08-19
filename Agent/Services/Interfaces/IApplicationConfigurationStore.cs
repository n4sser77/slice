using Slice.Common.Models;

namespace Agent.Services.Interfaces;

public interface IApplicationConfigurationStore
{
  Task<ApplicationConfiguration> LoadAsync(string applicationName, CancellationToken cancellationToken = default);
  Task ReplaceAsync(string applicationName, ApplicationConfiguration configuration, CancellationToken cancellationToken = default);
  Task DeleteAsync(string applicationName);
}
