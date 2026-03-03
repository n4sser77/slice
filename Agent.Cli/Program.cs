using System.Diagnostics;
using System.Runtime.InteropServices.Marshalling;
using System.Text.RegularExpressions;

string? filepath = null;

if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.WriteLine("Error: Provide a target app for deployment");
    return 1;
}
string pwd = Directory.GetCurrentDirectory();
string targetName = args[0];
var files = Directory.EnumerateFiles(pwd, $"{targetName}.*");


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

    using HttpClient client = new HttpClient
    {
        BaseAddress = new("http://localhost:5165")
    };
    using var MultipartContent = new MultipartFormDataContent();

    var fileStream = File.OpenRead(filepath);
    var filecontent = new StreamContent(fileStream);
    // client.PostAsync();



}
catch (OperationCanceledException)
{
    process.Kill(entireProcessTree: true);
    Console.WriteLine("Build process terminated by user");
    return 1;
}
return 0;




