using System.Text.Json.Serialization;
using Slice.Common.Models;

namespace Agent.Cli.Serialization;

[JsonSerializable(typeof(SystemdService))]
[JsonSerializable(typeof(List<SystemdService>))]
[JsonSerializable(typeof(ServiceStatus))]
[JsonSerializable(typeof(DeployResult))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
public partial class CliJsonContext : JsonSerializerContext { }
