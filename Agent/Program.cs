using System.Net;
using Agent.Configuration;
using Agent.Serialization;
using Agent.Services;
using Agent.Services.Exceptions;
using Agent.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Slice.Common.Models;


var builder = WebApplication.CreateSlimBuilder(args);


builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
  options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});

var systemdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config/systemd/user/");

builder.Services.AddTransient<FileNamingService>();
builder.Services.AddSingleton<IPortManager, PortManager>();
builder.Services.AddTransient(sp =>
        new ProcessManager(systemdPath, sp.GetRequiredService<IPortManager>()));

builder.Services.Configure<ReverseProxyOptions>(
    builder.Configuration.GetSection(ReverseProxyOptions.SectionName));

builder.Services.AddHttpClient<IReverseProxyClient, CaddyClient>((sp, client) =>
{
  var opts = sp.GetRequiredService<IOptions<ReverseProxyOptions>>().Value;
  client.BaseAddress = new Uri(opts.AdminUrl);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
}



app.MapPost("v1/services", [RequestSizeLimit(100_000_000)] async (
    IFormFile file,
    [FromForm] bool publish,
    [FromForm] string? domain,
    ProcessManager processRunner,
    FileNamingService namingService,
    IReverseProxyClient proxy,
    IOptions<ReverseProxyOptions> proxyOptions) =>
{
  string? appSafePath = null;
  try
  {
    appSafePath = namingService.GetSafeAppName(file.FileName);
    string displayName = namingService.GetRawAppName(file.FileName);
    string dllName = Path.GetFileNameWithoutExtension(file.FileName);
    var uploadPath = namingService.GetUploadPath(appSafePath);
    Directory.CreateDirectory(uploadPath);
    var z = new ZipExtractor();
    await z.ReadAndUnzip(file.OpenReadStream(), uploadPath);

    var dllPath = Path.Combine(Path.GetFullPath(uploadPath), dllName + ".dll");
    if (!File.Exists(dllPath))
    {
      return Results.Problem(detail: $"No runnable DLL '{dllName}.dll' found in uploaded archive.",
                             statusCode: (int)HttpStatusCode.BadRequest);
    }

    string? targetDomain = null;
    if (publish)
    {
      var opts = proxyOptions.Value;
      if (string.IsNullOrEmpty(opts.BaseDomain) && domain is null)
      {
        return Results.Problem(detail: "ReverseProxy:BaseDomain is not configured. Provide --domain explicitly.",
                               statusCode: (int)HttpStatusCode.BadRequest);
      }
      if (domain is not null && !namingService.IsDomainValid(domain))
      {
        return Results.Problem(detail: $"Provided domain '{domain}' is not valid.",
                               statusCode: (int)HttpStatusCode.BadRequest);
      }
      targetDomain = domain ?? $"{displayName}.{opts.BaseDomain}";
    }

    var port = await processRunner.CreateSystemdService(appSafePath, dllName, targetDomain);

    string? publicUrl = null;
    if (targetDomain is not null)
    {
      await proxy.RegisterRouteAsync(appSafePath, targetDomain, port);
      publicUrl = $"https://{targetDomain}";
    }

    return Results.Ok(new DeployResult(displayName, publicUrl));
  }
  catch (ArgumentException ex)
  {
    return Results.Problem(detail: ex.Message,
                           statusCode: (int)HttpStatusCode.BadRequest);
  }
  catch (InvalidDataException ex)
  {
    return Results.Problem(detail: ex.Message, statusCode: (int)HttpStatusCode.BadRequest);
  }
  catch (OutOfPortsException ex)
  {
    return Results.Problem(detail: ex.Message,
                           statusCode: (int)HttpStatusCode.ServiceUnavailable);
  }
  catch (SystemctlException ex)
  {
    return Results.Problem(detail: ex.Message,
                           statusCode: (int)HttpStatusCode.InternalServerError);
  }
  catch (HttpRequestException ex)
  {
    var rolled = await processRunner.StopServiceAsync(appSafePath!);
    var detail = rolled
        ? $"Caddy route registration failed: {ex.Message}. Service was rolled back."
        : $"Caddy route registration failed: {ex.Message}. Service rollback also failed — stop it manually.";
    return Results.Problem(detail: detail, statusCode: (int)HttpStatusCode.BadGateway);
  }
}).DisableAntiforgery();

app.MapGet("v1/services", async (ProcessManager processRunner) =>
{
  try
  {
    var services = await processRunner.GetServices();
    return Results.Ok(services);
  }
  catch (SystemctlException ex)
  {
    return Results.Problem(detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
  }
});

app.MapGet("v1/services/{serviceName}", async (string serviceName, ProcessManager processRunner) =>
{
  var fullName = $"{FileNamingService.FilePrefix}-{serviceName}";
  if (!FileNamingService.IsValidServiceName(fullName))
    return Results.Problem(detail: "Invalid service name.", statusCode: (int)HttpStatusCode.BadRequest);

  try
  {
    var service = await processRunner.GetServiceStatusAsync(fullName);
    if (service == null)
      return Results.NotFound();

    return Results.Ok(service);
  }
  catch (SystemctlException ex)
  {
    return Results.Problem(detail: ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
  }
});

app.MapPost("v1/services/{serviceName}/stop", async (string serviceName, ProcessManager processManager) =>
{
  var fullName = $"{FileNamingService.FilePrefix}-{serviceName}";
  if (!FileNamingService.IsValidServiceName(fullName))
    return Results.Problem(detail: "Invalid service name.", statusCode: (int)HttpStatusCode.BadRequest);

  var stopped = await processManager.StopServiceAsync(fullName);
  return stopped
      ? Results.NoContent()
      : Results.Problem(detail: $"Failed to stop service '{serviceName}'. Make sure the service exists and is running.",
                        statusCode: (int)HttpStatusCode.InternalServerError);
});

app.MapDelete("v1/services/{serviceName}", async (
    string serviceName,
    ProcessManager processRunner,
    IPortManager portManager,
    IReverseProxyClient proxy) =>
{
  var fullName = $"{FileNamingService.FilePrefix}-{serviceName}";
  if (!FileNamingService.IsValidServiceName(fullName))
    return Results.Problem(detail: "Invalid service name.", statusCode: (int)HttpStatusCode.BadRequest);

  var servicePath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
      ".config/systemd/user/", $"{fullName}.service");

  if (!File.Exists(servicePath))
  {
    await processRunner.ResetUnitCacheAsync();
    return Results.NoContent();
  }

  try
  {
    var port = processRunner.GetServicePortFromFile(fullName);

    await processRunner.DeleteServiceAsync(fullName);

    if (port is not null)
      portManager.ReleasePort(port.Value);

    try
    {
      await proxy.RemoveRouteAsync(fullName);
    }
    catch
    {
      // Caddy route may not exist — that's fine
    }

    return Results.NoContent();
  }
  catch (Exception ex)
  {
    return Results.Problem(detail: $"Failed to remove service '{serviceName}': {ex.Message}",
                           statusCode: (int)HttpStatusCode.InternalServerError);
  }
});

app.Run();
