namespace Agent.Configuration;

public sealed class ApplicationDataPaths
{
  private readonly string _sliceDataRoot;
  private readonly string _applicationsRoot;

  public ApplicationDataPaths(string? dataRoot = null)
  {
    var root = dataRoot ?? GetDefaultDataRoot();
    _sliceDataRoot = Path.Combine(root, "slice");
    _applicationsRoot = Path.Combine(_sliceDataRoot, "apps");
  }

  public string GetApplicationDirectory(string applicationName) =>
      Path.Combine(_applicationsRoot, applicationName);

  public string GetConfigurationFile(string applicationName) =>
      Path.Combine(GetApplicationDirectory(applicationName), "configuration.json");

  public string GetSystemdEnvironmentFile(string applicationName) =>
      Path.Combine(GetApplicationDirectory(applicationName), "runtime", "systemd.env");

  public void CreateApplicationDirectory(string applicationName) =>
      CreatePrivateDirectories(GetApplicationDirectory(applicationName));

  public void CreateRuntimeDirectory(string applicationName) =>
      CreatePrivateDirectories(Path.GetDirectoryName(GetSystemdEnvironmentFile(applicationName))!);

  private void CreatePrivateDirectories(string destination)
  {
    if (OperatingSystem.IsWindows())
    {
      Directory.CreateDirectory(destination);
      return;
    }

    CreatePrivateDirectory(_sliceDataRoot);
    CreatePrivateDirectory(_applicationsRoot);

    var relativePath = Path.GetRelativePath(_applicationsRoot, destination);
    var current = _applicationsRoot;
    foreach (var segment in relativePath.Split(Path.DirectorySeparatorChar))
    {
      current = Path.Combine(current, segment);
      CreatePrivateDirectory(current);
    }
  }

  private static void CreatePrivateDirectory(string path)
  {
    if (OperatingSystem.IsWindows())
    {
      Directory.CreateDirectory(path);
      return;
    }

    const UnixFileMode privateMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    if (!Directory.Exists(path))
      Directory.CreateDirectory(path, privateMode);
    File.SetUnixFileMode(path, privateMode);
  }

  private static string GetDefaultDataRoot()
  {
    var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
    var hasAbsoluteXdgDataHome =
        !string.IsNullOrWhiteSpace(xdgDataHome) && Path.IsPathFullyQualified(xdgDataHome);
    if (hasAbsoluteXdgDataHome)
      return xdgDataHome!;

    return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local",
        "share");
  }
}
