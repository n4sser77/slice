using System.Text.RegularExpressions;

namespace Agent.Utils;

public partial class StringHelper
{
    // Genererar koden vid build-time
    [GeneratedRegex(@"[^a-zA-Z0-9-]")]
    private static partial Regex CleanStringRegex();

    public string MakeSafe(string name)
    {
        // Använd den genererade metoden
        return CleanStringRegex().Replace(name, "");
    }
}
