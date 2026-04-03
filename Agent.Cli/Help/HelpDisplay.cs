namespace Agent.Cli.Help;

public static class HelpDisplay
{
    public static void ShowError(string? error = null)
    {
        if (error is { })
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Error: {error}");
            Console.ResetColor();
            Console.WriteLine();
        }
        Show();
    }

    public static void Show()
    {
        Console.WriteLine("Usage: agent-cli <target>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --help    Show this help message");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  deploy         Deploy a .NET application (default)");
    }
}