using System.Text.Json.Serialization;
using Slice.Common.Models;

namespace Agent.Cli.Serialization;

[JsonSerializable(typeof(SystemdService))]
[JsonSerializable(typeof(List<SystemdService>))]
[JsonSerializable(typeof(ServiceStatus))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
public partial class CliJsonContext : JsonSerializerContext { }
