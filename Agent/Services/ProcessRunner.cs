using System.Text.RegularExpressions;

namespace Agent.Services;

public class ProcessRunner
{
    private readonly string _targetDir;
    public ProcessRunner(string targetDir)
      => _targetDir = targetDir;

    public void CreateSystemdService(string appName)
    {
        string safeName = SanitizeName(appName);
        string appDir = Path.Combine("slice", safeName);
        string serviceContent =
            $@"
            [Unit]
            Description=Uploaded C# Service: {safeName}

            [Service]
            WorkingDirectory={appDir}
            ExecStart=/usr/bin/dotnet {appDir}/{safeName}.dll
            Restart=always
            DynamicUser=yes
            NoNewPrivileges=true
            PrivateTmp=true

            [Install]
            WantedBy=multi-user.target";

        var servicePath = Path.Combine(_targetDir, $"{safeName}.service");
        File.WriteAllText(servicePath, serviceContent.Trim());
    }

    private static string SanitizeName(string name)
    {
        var safe = Regex.Replace(name, @"[^a-zA-Z0-9-]", "");
        if (safe.Contains("..") || safe.StartsWith('/'))
            safe = safe.Replace("..", "").TrimStart('/');
        return string.IsNullOrEmpty(safe) ? "unnamed" : safe;
    }
}
