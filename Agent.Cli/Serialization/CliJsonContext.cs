using System.Text.Json.Serialization;
using Slice.Common.Models;

namespace Agent.Cli.Serialization;

[JsonSerializable(typeof(SystemdService))]
[JsonSerializable(typeof(List<SystemdService>))]
[JsonSerializable(typeof(ServiceStatus))]
[JsonSerializable(typeof(DeployResult))]
[JsonSerializable(typeof(ApplicationConfiguration))]
[JsonSerializable(typeof(AgentCapabilities))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class CliJsonContext : JsonSerializerContext { }
