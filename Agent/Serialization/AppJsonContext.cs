using System.Text.Json.Serialization;
using Slice.Common.Models;
using Microsoft.AspNetCore.Mvc;
using Agent.Services;

namespace Agent.Serialization;

[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(AppService))]
[JsonSerializable(typeof(List<AppService>))]
[JsonSerializable(typeof(SystemdService))]
[JsonSerializable(typeof(List<SystemdService>))]
[JsonSerializable(typeof(ServiceStatus))]
[JsonSerializable(typeof(DeployResult))]
[JsonSerializable(typeof(CaddyRoute))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
public partial class AppJsonContext : JsonSerializerContext { }

