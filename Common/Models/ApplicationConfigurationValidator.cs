using System.Text;

namespace Slice.Common.Models;

public static class ApplicationConfigurationValidator
{
  public const int MaximumEntries = 128;
  public const int MaximumKeyLength = 256;
  public const int MaximumValueBytes = 64 * 1024;
  public const int MaximumTotalBytes = 256 * 1024;

  public static string? Validate(ApplicationConfiguration configuration)
  {
    if (configuration.SchemaVersion != ApplicationConfiguration.CurrentSchemaVersion)
      return $"Unsupported application configuration schema version '{configuration.SchemaVersion}'.";

    if (configuration.Values is null)
      return "Application configuration values cannot be null.";
    if (configuration.Values.Count > MaximumEntries)
      return $"Application configuration cannot contain more than {MaximumEntries} entries.";

    var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var totalBytes = 0;
    foreach (var (key, value) in configuration.Values)
    {
      var keyError = ValidateKey(key);
      if (keyError is not null)
        return keyError;

      if (!keys.Add(key))
        return $"Application configuration contains keys that differ only by casing: '{key}'.";

      var valueError = ValidateValue(key, value, out var valueBytes);
      if (valueError is not null)
        return valueError;

      totalBytes += Encoding.UTF8.GetByteCount(key) + valueBytes;
      if (totalBytes > MaximumTotalBytes)
        return $"Application configuration exceeds {MaximumTotalBytes} UTF-8 bytes in total.";
    }

    return null;
  }

  public static string? ValidateKey(string key)
  {
    if (string.IsNullOrEmpty(key))
      return "Application configuration keys cannot be empty.";
    if (key.Length > MaximumKeyLength)
      return $"Application configuration key '{key}' exceeds {MaximumKeyLength} characters.";
    if (key.Contains("__", StringComparison.Ordinal))
      return $"Application configuration key '{key}' cannot contain '__'; use ':' for hierarchy.";

    var segments = key.Split(':');
    var hasInvalidFirstSegment = segments[0].Length == 0 || !IsNameStart(segments[0][0]);
    if (hasInvalidFirstSegment)
      return $"Application configuration key '{key}' must start with a letter or underscore.";
    if (segments.Any(HasInvalidSegment))
      return $"Application configuration key '{key}' contains an invalid segment.";
    if (IsReservedRuntimeKey(segments[0]))
      return $"Application configuration key '{key}' is reserved for the Slice runtime.";

    return null;
  }

  private static string? ValidateValue(string key, string value, out int valueBytes)
  {
    valueBytes = 0;
    if (value is null)
      return $"Application configuration value for '{key}' cannot be null.";
    if (value.Contains('\0'))
      return $"Application configuration value for '{key}' cannot contain a null character.";

    try
    {
      valueBytes = new UTF8Encoding(false, true).GetByteCount(value);
    }
    catch (EncoderFallbackException)
    {
      return $"Application configuration value for '{key}' is not valid Unicode.";
    }

    return valueBytes > MaximumValueBytes
        ? $"Application configuration value for '{key}' exceeds {MaximumValueBytes} UTF-8 bytes."
        : null;
  }

  private static bool IsNameStart(char character) =>
      character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_';

  private static bool IsNameCharacter(char character) =>
      IsNameStart(character) || character is >= '0' and <= '9';

  private static bool HasInvalidSegment(string segment) =>
      segment.Length == 0 || !segment.All(IsNameCharacter);

  private static bool IsReservedRuntimeKey(string root) =>
      root.Equals("ASPNETCORE", StringComparison.OrdinalIgnoreCase) ||
      root.StartsWith("ASPNETCORE_", StringComparison.OrdinalIgnoreCase) ||
      root.Equals("DOTNET", StringComparison.OrdinalIgnoreCase) ||
      root.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase);
}
