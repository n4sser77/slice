using System.Text.Json.Serialization;
using Slice.Common.Models;
using Microsoft.AspNetCore.Mvc;
using Agent.Services;
using Agent.Configuration;

namespace Agent.Serialization;

[JsonSerializable(typeof(ReverseProxyOptions))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(AppService))]
[JsonSerializable(typeof(List<AppService>))]
[JsonSerializable(typeof(SystemdService))]
[JsonSerializable(typeof(List<SystemdService>))]
[JsonSerializable(typeof(ServiceStatus))]
[JsonSerializable(typeof(DeployResult))]
[JsonSerializable(typeof(CaddyRoute))]
[JsonSerializable(typeof(IFormFile))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class AppJsonContext : JsonSerializerContext { }

