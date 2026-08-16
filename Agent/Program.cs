using System.Net;
using Agent.Auth;
using Agent.Configuration;
using Agent.Serialization;
using Agent.Services;
using Agent.Services.Exceptions;
using Agent.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Slice.Common.Models;


var builder = WebApplication.CreateSlimBuilder(args);

// op => op.AddSchemaTransformer<FormFileSchemaTransformer>()
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options =>
{
  options.CustomizeProblemDetails = context =>
      context.ProblemDetails.Extensions.TryAdd(
          "traceId",
          context.HttpContext.TraceIdentifier);
});
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

builder.Services.AddAuthentication(ApiKeyAuthOptions.SchemeName)
  .AddScheme<ApiKeyAuthOptions, ApiKeyAuthHandler>(ApiKeyAuthOptions.SchemeName, null);

builder.Services.AddAuthorization();
var app = builder.Build();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Agent.Api");

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
  app.MapScalarApiReference();
}
app.UseAuthentication();
app.UseAuthorization();

var servicesRoutes = app.MapGroup("/v1/services").RequireAuthorization();


servicesRoutes.MapPost("", [RequestSizeLimit(100_000_000)] async (
    HttpContext context,
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
    string dllName = namingService.GetRawAppName(file.FileName);
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
    return InternalProblem(
        context,
        logger,
        ex,
        "Failed to create the systemd service.");
  }
  catch (HttpRequestException ex)
  {
    var rolledBack = false;
    try
    {
      rolledBack = await processRunner.StopServiceAsync(appSafePath!);
    }
    catch (Exception rollbackException)
    {
      logger.LogError(
          rollbackException,
          "Service rollback failed. TraceId: {TraceId}",
          context.TraceIdentifier);
    }

    logger.LogError(
        ex,
        "Reverse-proxy route registration failed. TraceId: {TraceId}",
        context.TraceIdentifier);

    var detail = rolledBack
        ? "Reverse-proxy registration failed and the service was rolled back."
        : "Reverse-proxy registration failed and service rollback was unsuccessful.";
    return ExternalDependencyProblem(context, detail);
  }
}).DisableAntiforgery();

servicesRoutes.MapGet("", async (HttpContext context, ProcessManager processRunner) =>
{
  try
  {
    var servicesList = await processRunner.GetServices();
    return Results.Ok(servicesList);
  }
  catch (SystemctlException ex)
  {
    return InternalProblem(context, logger, ex, "Failed to list systemd services.");
  }
});

servicesRoutes.MapGet("/{serviceName}", async (
    HttpContext context,
    string serviceName,
    ProcessManager processRunner) =>
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
    return InternalProblem(context, logger, ex, "Failed to inspect the systemd service.");
  }
});

servicesRoutes.MapPost("/{serviceName}/stop", async (string serviceName, ProcessManager processManager) =>
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

servicesRoutes.MapDelete("/{serviceName}", async (
    HttpContext context,
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
    catch (HttpRequestException ex)
    {
      logger.LogWarning(
          ex,
          "Reverse-proxy route cleanup failed for {ServiceName}. TraceId: {TraceId}",
          fullName,
          context.TraceIdentifier);
    }

    return Results.NoContent();
  }
  catch (Exception ex)
  {
    return InternalProblem(
        context,
        logger,
        ex,
        "Failed to remove the systemd service.");
  }
});

app.Run();

static IResult InternalProblem(
    HttpContext context,
    ILogger logger,
    Exception exception,
    string operation)
{
  logger.LogError(
      exception,
      "{Operation} TraceId: {TraceId}",
      operation,
      context.TraceIdentifier);

  return Results.Problem(
      statusCode: StatusCodes.Status500InternalServerError,
      title: "An internal server error occurred.",
      detail: "The request could not be completed. Use the trace ID when contacting support.",
      extensions: new Dictionary<string, object?>
      {
        ["traceId"] = context.TraceIdentifier
      });
}

static IResult ExternalDependencyProblem(HttpContext context, string detail)
{
  return Results.Problem(
      statusCode: StatusCodes.Status502BadGateway,
      title: "A required external service failed.",
      detail: detail,
      extensions: new Dictionary<string, object?>
      {
        ["traceId"] = context.TraceIdentifier
      });
}
