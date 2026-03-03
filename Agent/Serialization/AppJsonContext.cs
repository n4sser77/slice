using System.Text.Json.Serialization;
using Agent.Models;

namespace Agent.Serialization;

[JsonSerializable(typeof(AppService))]
[JsonSerializable(typeof(List<AppService>))]
[JsonSerializable(typeof(SystemdService))]
[JsonSerializable(typeof(List<SystemdService>))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
public partial class AppJsonContext : JsonSerializerContext { }

