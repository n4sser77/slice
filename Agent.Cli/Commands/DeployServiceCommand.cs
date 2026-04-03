using System.Diagnostics;
using System.Net.Http;
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

        string pwd = Directory.GetCurrentDirectory();
        string? targetDir = Path.GetFileName(targetName).Contains('.') ? null : Path.Combine(pwd, targetName);
        var searchPattern = $"{Path.GetFileName(targetName)}.*";
        var searchDir = targetDir ?? pwd;
        string? filePath = null;

        foreach (var f in Directory.EnumerateFiles(searchDir, searchPattern))
        {
            if (f.EndsWith(".csproj") || f.EndsWith(".cs"))
            {
                filePath = f;
                break;
            }
        }

        if (filePath is null)
        {
            yield return new FinalResult(new ErrorResult($"No .cs or .csproj found for {targetName}", 1));
            yield break;
        }

        yield return new StepCompleted("Finding project files", TimeSpan.Zero);

        // Step 2: Build
        yield return new StepStarted("Building project");

        var buildResult = await RunBuildAsync(filePath, ct);
        if (buildResult.error is { } buildError)
        {
            yield return new FinalResult(buildError);
            yield break;
        }

        yield return new StepCompleted("Building project", TimeSpan.Zero);
        string publishPath = buildResult.output;

        // Step 3: Create zip
        yield return new StepStarted("Creating deployment package");

        var ms = new MemoryStream();
        using var arc = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);
        
        var files = Directory.GetFiles(publishPath);
        for (int i = 0; i < files.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var entry = arc.CreateEntry(Path.GetFileName(files[i]));
            await using var entryStream = entry.Open();
            await using var fs = File.OpenRead(files[i]);
            await fs.CopyToAsync(entryStream, ct);
            yield return new ProgressUpdate((i + 1) / (double)files.Length * 100, "Compressing");
        }

        ms.Position = 0;
        yield return new StepCompleted("Creating deployment package", TimeSpan.Zero);

        // Step 4: Upload
        yield return new StepStarted("Uploading to deployment service");

        using var client = new HttpClient { BaseAddress = new("http://localhost:5165/v1/"), Timeout = TimeSpan.FromSeconds(60) };
        using var content = new ProgressStreamContent(ms);
        using var multipart = new MultipartFormDataContent();
        multipart.Add(content, "file", $"{Path.GetFileNameWithoutExtension(filePath)}.zip");

        var uploadResult = await client.PostAsync("services", multipart, ct);
        
        if (!uploadResult.IsSuccessStatusCode)
        {
            var error = await uploadResult.Content.ReadAsStringAsync();
            yield return new FinalResult(new ErrorResult($"Upload failed: {uploadResult.StatusCode}", 1));
            yield break;
        }

        var responseBody = await uploadResult.Content.ReadAsStringAsync();
        yield return new StepCompleted("Uploading to deployment service", TimeSpan.Zero);
        yield return new FinalResult(new SuccessResult($"Deployment complete: {responseBody}"));
    }

    private async Task<(string output, ErrorResult? error)> RunBuildAsync(string filePath, CancellationToken ct)
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
            return ("", new ErrorResult("Build cancelled", 1));
        }

        if (process.ExitCode != 0)
            return ("", new ErrorResult("Build failed", 1));

        return (PathFinder.FindPublishPath(output.ToString()), null);
    }
}