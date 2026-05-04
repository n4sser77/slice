using System.Text.RegularExpressions;

namespace Agent.Services;

public partial class FileNamingService
{
  private const string AllowedExtension = ".zip";
  private const string FilePrefix = "slice";

  [GeneratedRegex(@"[^a-zA-Z0-9-]")]
  private static partial Regex SafeCharsRegex();

  public string GetSafeAppName(string filename) => $"{FilePrefix}-{GetRawAppName(filename)}";

  public string GetRawAppName(string filename)
  {
    var extension = Path.GetExtension(filename).ToLowerInvariant();
    if (extension != AllowedExtension)
      throw new ArgumentException($"Only {AllowedExtension} files are accepted.");

    var rawName = Path.GetFileNameWithoutExtension(filename);
    var cleanName = SafeCharsRegex().Replace(rawName, "").ToLowerInvariant();

    if (string.IsNullOrEmpty(cleanName))
      throw new ArgumentException("Filename cannot be empty after sanitization.");

    return cleanName;
  }

  public bool IsDomainValid(string? domain)
  {
    return Uri.CheckHostName(domain) == UriHostNameType.Dns;
  }

  public string GetUploadPath(string appName)
  {
    return Path.GetRelativePath(Directory.GetCurrentDirectory(), Path.Combine(FilePrefix, appName));
  }
}
