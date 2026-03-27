using System;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.NetworkInformation;
using Agent.Cli;

namespace Agent.Cli.Commands;

public interface ICommand
{
    Task<int> Execute();
}

public class DeployServiceCommand : ICommand
{
    private readonly string _targetName = "";
    private string? filepath = null;

    public DeployServiceCommand(ref string targetName)
    {
        _targetName = targetName;
    }
    public async Task<int> Execute()
    {

        string pwd = Directory.GetCurrentDirectory();
        string targetDir = "";

        if (IsTargetADirectory(_targetName))
        {
            targetDir = Path.Combine(pwd, _targetName); ;
        }

        var searchPattern = $"{_targetName}.*";
        var searchDir = string.IsNullOrWhiteSpace(targetDir) ?
            pwd : Path.Combine(pwd, targetDir);

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
            Console.WriteLine($"Error: No .cs or .csproj found for {_targetName}");
            return 1;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\nAborting build...");
        };
        // var archRpi = "linux-arm64";
        var archLinux = "linux-x64";

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
        "-r", archLinux,
        "--self-contained","false",
        "-p:PublishAot=false",
    }
        };


        using var process = new Process { StartInfo = psi };
        string stdout = "";
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                Console.WriteLine(new string('-', 40));
                Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"SUCCESS");
                Console.ResetColor();
                Console.WriteLine($": {Path.GetFileName(filepath)}");

                Console.WriteLine(string.Join(Environment.NewLine, e.Data.Split('\n').Select(line => $"  > {line.Trim()}")));
                stdout += e.Data;
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED");
                Console.WriteLine("Errors:");
                Console.WriteLine(string.Join(Environment.NewLine, e.Data.Split('\n').Select(line => $"  > {line.Trim()}")));
            }
        };

        try
        {
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            await process.WaitForExitAsync(cts.Token);
            if (process.ExitCode != 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("build failed!");
                return 1;
            }

            string publishPath = Utils.FindPublishPath(stdout);


            using var ms = new MemoryStream();

            using (var arc = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var srcFolder = publishPath;
                foreach (var srcFolderfilepath in Directory.GetFiles(srcFolder))
                {
                    var entry = arc.CreateEntry(srcFolderfilepath);
                    using var entryStream = await entry.OpenAsync();
                    using var fs = File.OpenRead(srcFolderfilepath);
                    await fs.CopyToAsync(entryStream);
                }
            }
            ms.Position = 0;



            // ZipFile.CreateFromDirectory(publishPath, Path.Combine(targetPath, targetZip));

            using HttpClient client = new HttpClient
            {
                BaseAddress = new("http://localhost:5165/v1/"),
                Timeout = TimeSpan.FromSeconds(60)
            };
            using var multipartContent = new MultipartFormDataContent();


            using var fileContent = new StreamContent(ms);
            multipartContent.Add(fileContent, "file", $"{Path.GetFileNameWithoutExtension(filepath)}.zip");

            var res = await client.PostAsync("services", multipartContent);

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


    }

    private bool IsTargetADirectory(string targetName)
    {
        return !targetName.EndsWith(".cs") && !targetName.StartsWith("/");
    }
}

public class ArgParser
{
    public ICommand? ParseArgs(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("Error: Provide a target app for deployment");
            return null;
        }

        if (args.Length == 1)
        {
            return new DeployServiceCommand(ref args[0]);
        }

        return null;

    }

}
