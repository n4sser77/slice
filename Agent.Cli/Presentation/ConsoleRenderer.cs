using System.Diagnostics;
using Agent.Cli.Core;
using Agent.Cli.Core.Events;
using Agent.Cli.Core.Results;

namespace Agent.Cli.Presentation;

public static class ConsoleRenderer
{
    public static async Task<int> RenderAsync(
        IAsyncEnumerable<ExecutionEvent> events,
        CancellationToken ct = default)
    {
        await foreach (var evt in events.WithCancellation(ct))
        {
            switch (evt)
            {
                case StepStarted s:
                    Console.Write($"[WAIT] {s.Name}... ");
                    break;

                case StepCompleted c:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\r[OK]   {c.Name} ({c.Duration.TotalSeconds:F1}s)");
                    Console.ResetColor();
                    break;

                case StatusMessage m:
                    Console.WriteLine($"  → {m.Message}");
                    break;

                case ProgressUpdate p:
                    Console.Write($"\r  → {p.Message}: {p.Percentage:F0}%");
                    break;

                case FinalResult f:
                    return RenderFinalResult(f.Result);
            }
        }
        return 0;
    }

    private static int RenderFinalResult(CommandResult result)
    {
        Console.WriteLine();
        if (result is SuccessResult s)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ {s.Message}");
            Console.ResetColor();
            return 0;
        }
        if (result is ErrorResult e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"✗ {e.Message}");
            Console.ResetColor();
            return e.ExitCode;
        }
        return 1;
    }
}