using System.Text.RegularExpressions;

namespace Agent.Services;

public partial class FileNamingService : IFileNamingService
{
    // private const string AllowedExtension = ".dll";
    private const string FilePrefix = "slice";

    [GeneratedRegex(@"[^a-zA-Z0-9-]")]
    private static partial Regex SafeCharsRegex();

    public string GetSafeAppName(string fileName)
    {
        // var extension = Path.GetExtension(fileName).ToLowerInvariant();
        // if (extension != AllowedExtension)
        //     throw new ArgumentException($"Only {AllowedExtension} files are accepted.");

        var rawName = Path.GetFileNameWithoutExtension(fileName);
        var cleanName = SafeCharsRegex().Replace(rawName, "").ToLowerInvariant();

        if (string.IsNullOrEmpty(cleanName))
            throw new ArgumentException("Filename cannot be empty after sanitization.");

        return $"{FilePrefix}-{cleanName}";
    }

    public string GetUploadPath(string appName)
    {
        return Path.Combine(FilePrefix, appName);
    }
}
