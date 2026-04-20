using Agent.Cli.Core.Events;
using Agent.Cli.Core.Results;
using Spectre.Console;

namespace Agent.Cli.Presentation;

public static class ConsoleRenderer
{
    public static async Task<int> RenderAsync(
        IAsyncEnumerable<ExecutionEvent> events,
        CancellationToken ct = default)
    {
        int exitCode = 0;

        await foreach (var evt in events.WithCancellation(ct))
        {
            exitCode = evt switch
            {
                StepStarted s => RenderStepStarted(s),
                StepCompleted c => RenderStepCompleted(c),
                StepFailed f => RenderStepFailed(f),
                StatusMessage m => RenderStatusMessage(m),
                ProgressUpdate p => RenderProgressUpdate(p),
                ServicesListed s => RenderServicesListed(s),
                DebugEvent d => RenderDebugEvent(d),
                FinalResult f => RenderFinalResult(f),
                _ => 0
            };
        }

        return exitCode;
    }

    private static int RenderStepStarted(StepStarted s)
    {
        Console.Write($"[WAIT] {s.Name}... ");
        return 0;
    }

    private static int RenderStepCompleted(StepCompleted c)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\r[OK]   {c.Name} ({c.Duration.TotalSeconds:F1}s)");
        Console.ResetColor();
        return 0;
    }

    private static int RenderStepFailed(StepFailed f)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\r[FAIL] {f.Name}: {f.Error}");
        Console.ResetColor();
        return 0;
    }

    private static int RenderStatusMessage(StatusMessage m)
    {
        Console.WriteLine($"  → {m.Message}");
        return 0;
    }

    private static int RenderProgressUpdate(ProgressUpdate p)
    {
        Console.Write($"\r  → {p.Message}: {p.Percentage:F0}%");
        return 0;
    }

    private static int RenderDebugEvent(DebugEvent d)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  [DEBUG] {d.Message}");
        Console.ResetColor();
        return 0;
    }

    private static int RenderServicesListed(ServicesListed listed)
    {
        if (listed.Services.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No slice services found.[/]");
            return 0;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("UNIT");
        table.AddColumn("ACTIVE");
        table.AddColumn("SUB");
        table.AddColumn("DESCRIPTION");

        foreach (var service in listed.Services.OrderBy(static s => s.Unit, StringComparer.Ordinal))
        {
            string activeStatus = service.Active ?? "-";

            if (activeStatus.Equals("active", StringComparison.OrdinalIgnoreCase))
            {
                activeStatus = $"[green]{activeStatus}[/]";
            }

            string subStatus = service.Sub ?? "-";
            if (subStatus.Equals("running", StringComparison.OrdinalIgnoreCase))
            {
                subStatus = $"[green]{subStatus}[/]";
            }
            else if (subStatus.Equals("failed", StringComparison.OrdinalIgnoreCase))
            {
                subStatus = $"[red]{subStatus}[/]";
            }

            table.AddRow(
                string.IsNullOrWhiteSpace(service.Unit) ? "-" : service.Unit,
                activeStatus,
                subStatus,
                string.IsNullOrWhiteSpace(service.Description) ? "-" : service.Description);
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private static int RenderFinalResult(FinalResult f)
    {
        Console.WriteLine();
        if (f.Result is SuccessResult s)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ {s.Message}");
            Console.ResetColor();
            return 0;
        }
        if (f.Result is ErrorResult e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"✗ {e.Message}");
            Console.ResetColor();
            return e.ExitCode;
        }
        return 1;
    }
}
