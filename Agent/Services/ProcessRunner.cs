namespace Agent.Services;

public class ProcessRunner
{
    private readonly string _targetDir;
    private readonly IFileNamingService _namingService;

    public ProcessRunner(IFileNamingService namingService, string targetDir)
    {
        _namingService = namingService;
        _targetDir = targetDir;
    }

    public void CreateSystemdService(string appName)
    {
        string appDir = Path.Combine("slice", appName);
        string serviceContent =
            $@"
            [Unit]
            Description=Uploaded C# Service: {appName}

            [Service]
            WorkingDirectory={appDir}
            ExecStart=/usr/bin/dotnet {appDir}/{appName}.dll
            Restart=always
            DynamicUser=yes
            NoNewPrivileges=true
            PrivateTmp=true

            [Install]
            WantedBy=multi-user.target";

        var servicePath = Path.Combine(_targetDir, $"{appName}.service");
        File.WriteAllText(servicePath, serviceContent.Trim());
    }
}
