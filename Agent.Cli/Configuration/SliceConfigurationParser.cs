using System.Text;
using Slice.Common.Models;

namespace Agent.Cli.Configuration;

internal static class SliceConfigurationParser
{
  public static ApplicationConfiguration Parse(
      string? filePath,
      IReadOnlyList<string> commandLineEntries)
  {
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    if (filePath is not null)
    {
      var fullPath = Path.GetFullPath(filePath);
      var lines = File.ReadAllLines(fullPath, new UTF8Encoding(false, true));
      for (var index = 0; index < lines.Length; index++)
      {
        var line = lines[index];
        if (index == 0)
          line = line.TrimStart('\uFEFF');

        var trimmed = line.Trim();
        var isBlankOrComment = trimmed.Length == 0 || trimmed.StartsWith('#');
        if (isBlankOrComment)
          continue;
        if (trimmed.StartsWith("export ", StringComparison.Ordinal))
          throw new FormatException($"{fullPath}:{index + 1}: 'export' is not supported.");

        var (key, value) = ParseEntry(line, $"{fullPath}:{index + 1}", allowComment: true);
        if (!values.TryAdd(key, value))
          throw new FormatException($"{fullPath}:{index + 1}: duplicate key '{key}'.");
      }
    }

    var commandLineKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var entry in commandLineEntries)
    {
      var (key, value) = ParseEntry(entry, "--config", allowComment: false);
      if (!commandLineKeys.Add(key))
        throw new FormatException($"--config contains duplicate key '{key}'.");
      values[key] = value;
    }

    var configuration = new ApplicationConfiguration(
        ApplicationConfiguration.CurrentSchemaVersion,
        new Dictionary<string, string>(values, StringComparer.Ordinal));
    var error = ApplicationConfigurationValidator.Validate(configuration);
    if (error is not null)
      throw new FormatException(error);

    return configuration;
  }

  private static (string Key, string Value) ParseEntry(string input, string source, bool allowComment)
  {
    var equalsIndex = input.IndexOf('=');
    if (equalsIndex < 0)
      throw new FormatException($"{source}: expected KEY=VALUE.");

    var key = input[..equalsIndex].Trim();
    var rawValue = input[(equalsIndex + 1)..].Trim();
    if (rawValue.Length == 0)
      return (key, string.Empty);

    if (rawValue[0] is '\'' or '"')
      return (key, ParseQuotedValue(rawValue, source, allowComment));

    if (allowComment)
    {
      for (var index = 1; index < rawValue.Length; index++)
      {
        if (rawValue[index] == '#' && char.IsWhiteSpace(rawValue[index - 1]))
          return (key, rawValue[..index].TrimEnd());
      }
    }

    return (key, rawValue);
  }

  private static string ParseQuotedValue(string input, string source, bool allowComment)
  {
    var quote = input[0];
    var result = new StringBuilder();
    var escaped = false;
    var closingIndex = -1;

    for (var index = 1; index < input.Length; index++)
    {
      var character = input[index];
      if (escaped)
      {
        result.Append(character switch
        {
          '\\' => '\\',
          '"' => '"',
          '\'' => '\'',
          'n' => '\n',
          'r' => '\r',
          't' => '\t',
          _ => throw new FormatException($"{source}: unsupported escape sequence '\\{character}'.")
        });
        escaped = false;
        continue;
      }

      if (character == '\\')
      {
        escaped = true;
        continue;
      }
      if (character == quote)
      {
        closingIndex = index;
        break;
      }
      result.Append(character);
    }

    if (escaped || closingIndex < 0)
      throw new FormatException($"{source}: unterminated quoted value.");

    var remainder = input[(closingIndex + 1)..];
    var isWhitespaceSeparatedComment = allowComment &&
        remainder.Length > 0 &&
        char.IsWhiteSpace(remainder[0]) &&
        remainder.TrimStart().StartsWith('#');
    if (remainder.Length > 0 && !isWhitespaceSeparatedComment)
      throw new FormatException($"{source}: unexpected content after quoted value.");

    return result.ToString();
  }
}
