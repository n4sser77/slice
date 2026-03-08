using System.Diagnostics;
using System.IO.Compression;
using Agent.Cli;

//write cw hello world, or a welcome text
string? filepath = null;

if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.WriteLine("Error: Provide a target app for deployment");
    return 1;
}
string pwd = Directory.GetCurrentDirectory();
string targetName = args[0];
string targetDir = "";

if (IsTargetADirectory(targetName))
{
    targetDir = Path.Combine(pwd, targetName);
}
var searchPattern = $"{targetName}.*";
var searchDir = string.IsNullOrWhiteSpace(targetDir) ? pwd : Path.Combine(pwd, targetDir);
var files = Directory.EnumerateFiles(searchDir, searchPattern);
foreach (var f in files)
{
    if (f.EndsWith(".csproj"))
    {
        filepath = f;
    }
    else if (f.EndsWith(".cs"))
    {
        filepath = f;
    }
}

if (string.IsNullOrWhiteSpace(filepath))
{
    Console.WriteLine($"Error: No .cs or .csproj found for {targetName}");
    return 1;
}
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\nAborting build...");
};


var psi = new ProcessStartInfo
{
    FileName = "dotnet",
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
    CreateNoWindow = true,
    ArgumentList = {
        "publish",
        filepath,
        "-c", "Release",
        "-r", "linux-arm64",
        "--self-contained","false",
        "-p:PublishAot=false",
    }
};


// Create the process instance so we can track it
using var process = new Process { StartInfo = psi };
try
{
    process.Start();

    await process.WaitForExitAsync(cts.Token);
    var statusColor = process.ExitCode == 0 ? ConsoleColor.Green : ConsoleColor.Red;
    var statusText = process.ExitCode == 0 ? "SUCCESS" : "FAILED";

    Console.WriteLine(new string('-', 40));
    Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
    Console.ForegroundColor = statusColor;
    Console.Write($"{statusText} ");
    Console.ResetColor();
    Console.WriteLine($": {Path.GetFileName(filepath)}");

    if (process.ExitCode != 0)
    {
        var error = await process.StandardError.ReadToEndAsync(cts.Token);
        Console.WriteLine("Errors:");
        Console.WriteLine(string.Join(Environment.NewLine, error.Split('\n').Select(line => $"  > {line.Trim()}")));
    }

    var stdout = await process.StandardOutput.ReadToEndAsync(cts.Token);
    Console.WriteLine("Details:");
    // Indent every line of stdout by 2 spaces
    Console.WriteLine(string.Join(Environment.NewLine, stdout.Split('\n').Select(line => $"  | {line.Trim()}")));
    Console.WriteLine(new string('-', 40));


    string publishPath = Utils.FindPublishPath(stdout);
    string targetPath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(filepath));
    if (File.Exists(targetPath))
        File.Delete(targetPath);

    ZipFile.CreateFromDirectory(publishPath, targetPath);

    using HttpClient client = new HttpClient
    {
        BaseAddress = new("http://localhost:5165"),
        Timeout = TimeSpan.FromSeconds(60)
    };
    using var multipartContent = new MultipartFormDataContent();

    using var fileContent = new StreamContent(File.OpenRead(targetPath));
    multipartContent.Add(fileContent, "file", Path.GetFileName(targetPath));

    var res = await client.PostAsync("/services", multipartContent);

    if (!res.IsSuccessStatusCode)
    {
        var error = await res.Content.ReadAsStringAsync();
        Console.WriteLine($"Upload failed: {res.StatusCode} - {error}");
        return 1;
    }

    var responseBody = await res.Content.ReadAsStringAsync();
    Console.WriteLine($"Upload successful: {responseBody}");
}
catch (OperationCanceledException)
{
    process.Kill(entireProcessTree: true);
    Console.WriteLine("Build process terminated by user");
    return 1;
}
catch (HttpRequestException e)
{
    process.Kill(entireProcessTree: true);
    Console.WriteLine($"Request to remote server failed: {e.StatusCode}, {e.Message} ");
    return 1;
}
return 0;

static bool IsTargetADirectory(string targetName)
{
    return !targetName.EndsWith(".cs") && !targetName.StartsWith("/");
}
