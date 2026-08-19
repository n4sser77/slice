using System.CommandLine;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Agent.Cli.Core;
using Agent.Cli.Core.Events;
using Agent.Cli.Core.Results;
using Agent.Cli.Configuration;
using Agent.Cli.Presentation;
using Agent.Cli.Serialization;
using Agent.Cli.Utils;
using Slice.Common.Models;

namespace Agent.Cli.Commands;

public class DeployServiceCommand(
    string targetName,
    bool publish,
    string? domain,
    HttpClient httpClient,
    CliConfig config,
    IReadOnlyList<string>? configurationEntries = null,
    string? configurationFile = null) : ICommand
{
  private readonly CliConfig _config = config;
  private readonly IReadOnlyList<string> _configurationEntries = configurationEntries ?? [];
  public static void Register(RootCommand root, HttpClient httpClient, CliConfig? config = null)
  {
    if (config is null)
    {
      throw new ArgumentNullException(nameof(config), "CliConfig is required to register DeployServiceCommand");
    }
    var targetArg = new Argument<string>("target") { Description = "The .NET project name or .csproj path to deploy" };
    var publishOpt = new Option<bool>("--publish") { Description = "Expose the app publicly via the reverse proxy" };
    var domainOpt = new Option<string?>("--domain") { Description = "Custom domain. Defaults to <appname>.<base-domain>" };
    var configurationOpt = new Option<string[]>("--config")
        { Description = "Application setting as KEY=VALUE. May be repeated" };
    var configurationFileOpt = new Option<string?>("--config-file")
        { Description = "Path to a Slice dotenv-style application configuration file" };

    var command = new Command("deploy", "Deploy a .NET service")
        { targetArg, publishOpt, domainOpt, configurationOpt, configurationFileOpt };

    command.SetAction(async (parseResult, ct) =>
    {
      var cmd = new DeployServiceCommand(
          parseResult.GetValue(targetArg)!,
          parseResult.GetValue(publishOpt),
          parseResult.GetValue(domainOpt),
          httpClient,
          config,
          parseResult.GetValue(configurationOpt) ?? [],
          parseResult.GetValue(configurationFileOpt)
          );
      return await ConsoleRenderer.RenderAsync(cmd.ExecuteStreamingAsync(ct), ct);
    });

    root.Subcommands.Add(command);
  }


  public async IAsyncEnumerable<ExecutionEvent> ExecuteStreamingAsync(
      [EnumeratorCancellation] CancellationToken ct = default)
  {
    var hasUnpublishedCustomDomain = domain is not null && !publish;
    if (hasUnpublishedCustomDomain)
    {
      yield return new FinalResult(new ErrorResult(
          "--domain requires --publish. Did you mean: slice deploy <target> --publish --domain <domain>?", 1));
      yield break;
    }

    ApplicationConfiguration? applicationConfiguration = null;
    var hasConfigurationInput = configurationFile is not null || _configurationEntries.Count > 0;
    if (hasConfigurationInput)
    {
      yield return new StepStarted("Reading application configuration");
      var configurationResult = TryReadApplicationConfiguration();
      if (configurationResult.Error is not null)
      {
        yield return new StepFailed("Reading application configuration", configurationResult.Error.Message);
        yield break;
      }
      applicationConfiguration = configurationResult.Configuration;
      yield return new StepCompleted("Reading application configuration", TimeSpan.Zero);

      yield return new StepStarted("Checking Agent capabilities");
      var capabilityError = await CheckConfigurationCapabilityAsync(ct);
      if (capabilityError is not null)
      {
        yield return new StepFailed("Checking Agent capabilities", capabilityError.Message);
        yield break;
      }
      yield return new StepCompleted("Checking Agent capabilities", TimeSpan.Zero);
    }

    yield return new StepStarted("Finding project files");
    var findResult = TryFindProject(ct);
    if (findResult.error is ErrorResult findError)
    {
      yield return new StepFailed("Finding project files", findError.Message);
      yield break;
    }
    yield return new StepCompleted("Finding project files", TimeSpan.Zero);

    yield return new StepStarted("Building project");
    var buildResult = await TryBuildProjectAsync(findResult.filePath!, ct);
    if (buildResult.error is ErrorResult buildError)
    {
      yield return new StepFailed("Building project", buildError.Message);
      yield break;
    }
    yield return new StepCompleted("Building project", TimeSpan.Zero);

    yield return new StepStarted("Creating deployment package");
    var packageResult = TryCreatePackage(buildResult.publishPath!, configurationFile);
    if (packageResult.error is ErrorResult packageError)
    {
      yield return new StepFailed("Creating deployment package", packageError.Message);
      yield break;
    }
    yield return new StepCompleted("Creating deployment package", TimeSpan.Zero);

    yield return new StepStarted("Uploading to deployment service");
    var uploadResult = await TryUploadAsync(
        packageResult.zipStream!, packageResult.fileName!, applicationConfiguration, ct);
    if (uploadResult.error is ErrorResult uploadError)
    {
      yield return new StepFailed("Uploading to deployment service", uploadError.Message);
      yield break;
    }
    yield return new StepCompleted("Uploading to deployment service", TimeSpan.Zero);

    var result = uploadResult.result;
    if (result is null)
    {
      yield return new StepFailed("Uploading to deployment service", "Unknown error, results are null");
      yield break;
    }
    var message = result.PublicUrl is { } url
        ? $"Deployed {result.AppName} → {url}"
        : $"Deployed {result.AppName} (localhost only)";

    yield return new FinalResult(new SuccessResult(message));
  }

  private (string? filePath, ErrorResult? error) TryFindProject(CancellationToken ct)
  {
    try
    {
      string pwd = Directory.GetCurrentDirectory();
      string? targetDir = Path.GetFileName(targetName).Contains('.') ? null : Path.Combine(pwd, targetName);
      var searchPattern = $"{Path.GetFileName(targetName)}.*";
      var searchDir = targetDir ?? pwd;

      foreach (var f in Directory.EnumerateFiles(searchDir, searchPattern))
      {
        ct.ThrowIfCancellationRequested();
        if (f.EndsWith(".csproj") || f.EndsWith(".cs"))
          return (f, null);
      }
      return (null, new ErrorResult($"No .cs or .csproj found for {targetName}", 1));
    }
    catch (OperationCanceledException)
    {
      return (null, new ErrorResult("Cancelled", 1));
    }
    catch (Exception ex)
    {
      return (null, new ErrorResult(ex.Message, 1));
    }
  }

  private async Task<(string? publishPath, ErrorResult? error)> TryBuildProjectAsync(string filePath, CancellationToken ct)
  {
    try
    {
      var output = new System.Text.StringBuilder();
      var psi = new ProcessStartInfo
      {
        FileName = "dotnet",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        ArgumentList = { "publish", filePath, "-c", "Release", "-r", _config.TargetHost, "--self-contained", "false", "-p:PublishAot=false" }
      };

      using var process = new Process { StartInfo = psi };
      process.OutputDataReceived += (s, e) => { if (e.Data is { } l) output.AppendLine(l); };
      process.ErrorDataReceived += (s, e) => { if (e.Data is { } l) output.AppendLine($"ERR: {l}"); };

      process.Start();
      process.BeginErrorReadLine();
      process.BeginOutputReadLine();

      var cancelled = false;
      try { await process.WaitForExitAsync(ct); }
      catch (OperationCanceledException) { cancelled = true; }

      if (cancelled)
      {
        process.Kill(entireProcessTree: true);
        return (null, new ErrorResult("Build cancelled", 1));
      }

      if (process.ExitCode != 0)
        return (null, new ErrorResult("Build failed", 1));

      return (PathFinder.FindPublishPath(output.ToString()), null);
    }
    catch (Exception ex)
    {
      return (null, new ErrorResult(ex.Message, 1));
    }
  }

  internal (MemoryStream? zipStream, string? fileName, ErrorResult? error) TryCreatePackage(
      string publishPath,
      string? selectedConfigurationFile = null)
  {
    try
    {
      var ms = new MemoryStream();
      using (var arc = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
      {
        var files = Directory.EnumerateFiles(
            publishPath,
            "*",
            SearchOption.AllDirectories);

        foreach (var file in files)
        {
          var candidateName = Path.GetFileName(file);
          if (ShouldExcludeFromPackage(candidateName, selectedConfigurationFile))
            continue;

          var relativePath = Path.GetRelativePath(publishPath, file)
              .Replace(Path.DirectorySeparatorChar, '/');
          var entry = arc.CreateEntry(relativePath);
          using var entryStream = entry.Open();
          using var fs = File.OpenRead(file);
          fs.CopyTo(entryStream);
        }
      }

      ms.Position = 0;
      string fileName = $"{Path.GetFileNameWithoutExtension(targetName)}.zip";

      return (ms, fileName, null);
    }
    catch (Exception ex)
    {
      return (null, null, new ErrorResult(ex.Message, 1));
    }
  }

  private (ApplicationConfiguration? Configuration, ErrorResult? Error)
      TryReadApplicationConfiguration()
  {
    try
    {
      return (SliceConfigurationParser.Parse(configurationFile, _configurationEntries), null);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
    {
      return (null, new ErrorResult(ex.Message, 1));
    }
  }

  private static bool ShouldExcludeFromPackage(
      string candidateName,
      string? selectedConfigurationFile) =>
      candidateName.Equals(".env.slice", StringComparison.OrdinalIgnoreCase) ||
      selectedConfigurationFile is not null &&
      candidateName.Equals(
          Path.GetFileName(selectedConfigurationFile),
          StringComparison.OrdinalIgnoreCase);

  private async Task<ErrorResult?> CheckConfigurationCapabilityAsync(CancellationToken ct)
  {
    try
    {
      using var response = await httpClient.GetAsync("capabilities", ct);
      if (!response.IsSuccessStatusCode)
      {
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
          return UnsupportedConfigurationError();
        return new ErrorResult(
            $"Agent capability check failed: {response.StatusCode}. Verify the Agent URL and API key.", 1);
      }

      var body = await response.Content.ReadAsStringAsync(ct);
      var capabilities = JsonSerializer.Deserialize(body, CliJsonContext.Default.AgentCapabilities);
      var supportsApplicationConfiguration = capabilities?.Features.Contains(
          AgentFeatures.ApplicationConfigurationV1,
          StringComparer.Ordinal) == true;
      if (!supportsApplicationConfiguration)
        return UnsupportedConfigurationError();

      return null;
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
    {
      return new ErrorResult($"Could not verify Agent configuration support: {ex.Message}", 1);
    }
  }

  private static ErrorResult UnsupportedConfigurationError() => new(
      "This Slice Agent does not support application configuration. Upgrade the Agent before deploying with --config or --config-file.", 1);

  private async Task<(DeployResult? result, ErrorResult? error)> TryUploadAsync(
      MemoryStream zipStream,
      string fileName,
      ApplicationConfiguration? applicationConfiguration,
      CancellationToken ct)
  {
    try
    {
      using var content = new ProgressStreamContent(zipStream);
      using var multipart = new MultipartFormDataContent();
      multipart.Add(content, "file", fileName);
      multipart.Add(new StringContent(publish ? "true" : "false"), "publish");
      if (domain is not null)
        multipart.Add(new StringContent(domain), "domain");
      if (applicationConfiguration is not null)
      {
        var json = JsonSerializer.Serialize(
            applicationConfiguration,
            CliJsonContext.Default.ApplicationConfiguration);
        multipart.Add(new StringContent(json), "configuration");
      }

      using var uploadResult = await httpClient.PostAsync("services", multipart, ct);

      if (!uploadResult.IsSuccessStatusCode)
      {
        var error = await uploadResult.Content.ReadAsStringAsync(ct);
        return (null, new ErrorResult($"Error: {uploadResult.StatusCode}. {error}"));
      }

      var body = await uploadResult.Content.ReadAsStringAsync(ct);
      var result = JsonSerializer.Deserialize(body, CliJsonContext.Default.DeployResult);
      return (result, null);
    }
    catch (HttpRequestException ex)
    {
      return (null, new ErrorResult($"Connection failed: {ex.Message}. Make sure the deployment service is running.", 1));
    }
    catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
    {
      return (null, new ErrorResult($"Connection timed out. Is the deployment service running at http://localhost:5165?, {ex.Message}", 1));
    }
    catch (JsonException ex)
    {
      return (null, new ErrorResult($"Server returned invalid JSON: {ex.Message}", 1));
    }
    catch (OperationCanceledException)
    {
      return (null, new ErrorResult("Cancelled", 1));
    }
    catch (Exception ex)
    {
      return (null, new ErrorResult(ex.Message, 1));
    }
  }

}
