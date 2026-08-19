namespace Slice.Common.Models;

public sealed record ApplicationConfiguration(int SchemaVersion, Dictionary<string, string> Values)
{
  public const int CurrentSchemaVersion = 1;

  public static ApplicationConfiguration Empty => new(CurrentSchemaVersion, []);
}
