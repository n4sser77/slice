using Agent.Cli.Core.Events;
using Agent.Cli.Core.Results;
using Spectre.Console;
using Slice.Common.Models;
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
        ServiceStatusReceived ss => RenderServiceStatus(ss),
        DebugEvent d => RenderDebugEvent(d),
        FinalResult f => RenderFinalResult(f),
        _ => 0
      };
    }

    return exitCode;
  }


  private static int RenderServiceStatus(ServiceStatusReceived ss)
  {
    var svc = ss.Service;
    var stateColor = svc.ActiveState == "active" ? "green" : "red";
    var subStateColor = svc.SubState == "running" ? "springgreen3" : "yellow";

    var table = new Table()
        .Border(TableBorder.Rounded)
        .Title($"[blue]Service Inspection:[/] [white]{svc.Id}[/]")
        .AddColumn("[grey]Property[/]")
        .AddColumn("[grey]Value[/]");

    table.AddRow("Description", $"[italic]{svc.Description}[/]");
    table.AddRow("Status", $"[{stateColor}]{svc.ActiveState}[/] ([{subStateColor}]{svc.SubState}[/])");
    table.AddRow("Main PID", $"[cyan]{svc.MainPid}[/]");

    double memMb = svc.MemoryCurrent / 1024.0 / 1024.0;
    double peakMb = svc.MemoryPeak / 1024.0 / 1024.0;
    table.AddRow("Memory", $"{memMb:F2} MB [grey](Peak: {peakMb:F2} MB)[/]");

    table.AddRow("CPU Usage", $"[yellow]{FormatCpuUsage(svc.CpuUsageNSec)}[/]");

    var elapsed = FormatElapsed(svc.StateChangeTimestamp);
    if (svc.ActiveState == "active")
    {
      table.AddRow("Uptime", $"[cyan]{elapsed}[/]");
    }
    else
    {
      table.AddRow("Down Since", $"[red]{svc.StateChangeTimestamp}[/] [grey]({elapsed} ago)[/]");
    }

    AnsiConsole.Write(table);
    return 0;
  }

  private const ulong NsPerMicrosecond = 1_000UL;
  private const ulong NsPerMillisecond = NsPerMicrosecond * 1_000UL;
  private const ulong NsPerSecond      = NsPerMillisecond * 1_000UL;

  private static string FormatCpuUsage(ulong ns)
  {
    if (ns < NsPerMicrosecond) return $"{ns}ns";
    if (ns < NsPerMillisecond) return $"{ns / (double)NsPerMicrosecond:F1}µs";
    if (ns < NsPerSecond)      return $"{ns / (double)NsPerMillisecond:F1}ms";

    var totalSeconds = ns / (double)NsPerSecond;
    if (totalSeconds < 60) return $"{totalSeconds:F2}s";

    var ts = TimeSpan.FromSeconds(totalSeconds);
    if (ts.TotalHours < 1) return $"{ts.Minutes}m {ts.Seconds}s";
    return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
  }

  private static string FormatElapsed(string timestamp)
  {
    // systemd format: "Mon 2026-04-27 10:27:19 CEST"
    var parts = timestamp.Split(' ');
    if (parts.Length < 3) return "unknown";

    if (!DateTime.TryParseExact(
            $"{parts[1]} {parts[2]}",
            "yyyy-MM-dd HH:mm:ss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeLocal,
            out var since))
      return "unknown";

    var elapsed = DateTime.Now - since;
    if (elapsed.TotalDays >= 1)    return $"{(int)elapsed.TotalDays}d {elapsed.Hours}h {elapsed.Minutes}m";
    if (elapsed.TotalHours >= 1)   return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m {elapsed.Seconds}s";
    if (elapsed.TotalMinutes >= 1) return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
    return $"{elapsed.Seconds}s";
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
