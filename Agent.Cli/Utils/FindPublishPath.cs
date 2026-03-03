namespace Agent.Cli;

public static class Utils
{
    // Details:
    //   | Determining projects to restore...
    //   | Restored /home/qanasser/projects/slice/Agent.Cli/Agen
    // t.Cli.csproj (in 942 ms).
    //   | Agent.Cli -> /home/qanasser/projects/slice/Agent.Cli/
    // bin/Release/net10.0/linux-arm64/Agent.Cli.dll
    //   | Agent.Cli -> /home/qanasser/projects/slice/Agent.Cli/
    // bin/Release/net10.0/linux-arm64/publish/
    //
    public static string FindPublishPath(string stdout)
    {
        int index = stdout.LastIndexOf("->");
        if (index == -1) return string.Empty;

        var brokenPath = stdout.Substring(index + 2);

        // Split by lines and join while trimming whitespace from each part
        string pathPart = string.Concat(brokenPath
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim()));

        // Note: If pathPart starts with "bin/...", GetFullPath
        // uses the test's execution directory.
        return Path.GetFullPath(pathPart);
    }
}

