namespace Slice.Common.Models;

public sealed record AgentCapabilities(string[] Features);

public static class AgentFeatures
{
  public const string ApplicationConfigurationV1 = "application-configuration-v1";
}
