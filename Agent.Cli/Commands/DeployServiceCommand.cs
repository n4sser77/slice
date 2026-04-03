using System.Diagnostics;
using System.IO.Compression;
using Agent.Cli.Core;
using Agent.Cli.Core.Events;
using Agent.Cli.Core.Results;
using Agent.Cli.Utils;

namespace Agent.Cli.Commands;

public class DeployServiceCommand(string targetName) : ICommand
{
    public async IAsyncEnumerable<ExecutionEvent> ExecuteStreamingAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Step 1: Find project
        yield return new StepStarted("Finding project files");
        var findResult = TryFindProject(ct);
        if (findResult.error is ErrorResult findError)
        {
            yield return new StepFailed("Finding project files", findError.Message);
            yield break;
        }
        yield return new StepCompleted("Finding project files", TimeSpan.Zero);

        // Step 2: Build
        yield return new StepStarted("Building project");
        var buildResult = await TryBuildProjectAsync(findResult.filePath!, ct);
        if (buildResult.error is ErrorResult buildError)
        {
            yield return new StepFailed("Building project", buildError.Message);
            yield break;
        }
        yield return new StepCompleted("Building project", TimeSpan.Zero);

        // Step 3: Create package
        yield return new StepStarted("Creating deployment package");
        var packageResult = TryCreatePackage(buildResult.publishPath!);
        if (packageResult.error is ErrorResult packageError)
        {
            yield return new StepFailed("Creating deployment package", packageError.Message);
            yield break;
        }
        yield return new StepCompleted("Creating deployment package", TimeSpan.Zero);

        // Step 4: Upload
        yield return new StepStarted("Uploading to deployment service");
        var uploadResult = await TryUploadAsync(packageResult.zipStream!, packageResult.fileName!, ct);
        if (uploadResult.error is ErrorResult uploadError)
        {
            yield return new StepFailed("Uploading to deployment service", uploadError.Message);
            yield break;
        }
        yield return new StepCompleted("Uploading to deployment service", TimeSpan.Zero);
        yield return new FinalResult(new SuccessResult($"Deployment complete: {uploadResult.response}"));
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
                ArgumentList = { "publish", filePath, "-c", "Release", "-r", "linux-x64", "--self-contained", "false", "-p:PublishAot=false" }
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

    private (MemoryStream? zipStream, string? fileName, ErrorResult? error) TryCreatePackage(string publishPath)
    {
        try
        {
            var ms = new MemoryStream();
            using var arc = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);

            var files = Directory.GetFiles(publishPath);
            foreach (var file in files)
            {
                var entry = arc.CreateEntry(Path.GetFileName(file));
                using var entryStream = entry.Open();
                using var fs = File.OpenRead(file);
                fs.CopyTo(entryStream);
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

    private async Task<(string? response, ErrorResult? error)> TryUploadAsync(MemoryStream zipStream, string fileName, CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient { BaseAddress = new("http://localhost:5165/v1/"), Timeout = TimeSpan.FromSeconds(60) };

            using var content = new ProgressStreamContent(zipStream);
            using var multipart = new MultipartFormDataContent();
            multipart.Add(content, "file", fileName);
            client.Timeout = TimeSpan.FromSeconds(10);
            var uploadResult = await client.PostAsync("services", multipart, ct);

            if (!uploadResult.IsSuccessStatusCode)
            {
                var error = await uploadResult.Content.ReadAsStringAsync();
                return (null, new ErrorResult($"Connection failed: {uploadResult.StatusCode}. Make sure the deployment service is running.", 1));
            }

            var response = await uploadResult.Content.ReadAsStringAsync();
            return (response, null);
        }
        catch (HttpRequestException ex)
        {
            return (null, new ErrorResult($"Connection failed: {ex.Message}. Make sure the deployment service is running.", 1));
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            return (null, new ErrorResult($"Connection timed out. Is the deployment service running at http://localhost:5165?, {ex.Message}", 1));
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
