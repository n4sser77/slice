namespace Agent.Cli.Utils;

public static class PathFinder
{
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

