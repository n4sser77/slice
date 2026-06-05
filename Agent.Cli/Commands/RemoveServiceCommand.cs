using System.CommandLine;
using System.Net;
using System.Runtime.CompilerServices;
using Agent.Cli.Core;
using Agent.Cli.Core.Events;
using Agent.Cli.Core.Results;
using Agent.Cli.Presentation;

namespace Agent.Cli.Commands;

public class RemoveServiceCommand(string serviceName, HttpClient httpClient) : ICommand
{
  public static void Register(RootCommand root, HttpClient httpClient, CliConfig? config = null)
  {
    Command command = new("remove", "Remove a deployed service and free its resources.");
    Argument<string> serviceNameArg = new("service-name")
    {
      Description = "The name of the service without the 'slice-' prefix and '.service' suffix."
    };
    command.Add(serviceNameArg);

    command.SetAction(async (parseResult, ct) =>
    {
      var raw = parseResult.GetValue(serviceNameArg)!;
      var name = raw.EndsWith(".service", StringComparison.OrdinalIgnoreCase) ? raw.Substring(0, raw.Length - ".service".Length) : raw;
      var cmd = new RemoveServiceCommand(name, httpClient);
      return await ConsoleRenderer.RenderAsync(cmd.ExecuteStreamingAsync(ct), ct);
    });

    root.Subcommands.Add(command);
  }

  public async IAsyncEnumerable<ExecutionEvent> ExecuteStreamingAsync(
      [EnumeratorCancellation] CancellationToken ct = default)
  {
    yield return new StepStarted("Removing service");

    var (_, err) = await TryRemoveService(ct);
    if (err is ErrorResult errorResult)
    {
      yield return new StepFailed("Removing service", errorResult.Message);
      yield return new FinalResult(errorResult);
      yield break;
    }

    yield return new StepCompleted("Removing service", TimeSpan.Zero);
    yield return new FinalResult(new SuccessResult($"Removed {serviceName}"));
  }

  private async Task<(bool removed, ErrorResult? err)> TryRemoveService(CancellationToken ct)
  {
    try
    {
      var response = await httpClient.DeleteAsync($"services/{serviceName}", ct);

      if (response.StatusCode == HttpStatusCode.NotFound)
        return (false, new ErrorResult($"Service '{serviceName}' not found.", 1));

      if (!response.IsSuccessStatusCode)
      {
        var detail = await response.Content.ReadAsStringAsync(ct);
        return (false, new ErrorResult($"Error from server: {response.StatusCode}. {detail}", 1));
      }

      return (true, null);
    }
    catch (HttpRequestException ex)
    {
      return (false, new ErrorResult($"Connection failed: {ex.Message}. Make sure the deployment service is running.", 1));
    }
    catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
    {
      return (false, new ErrorResult($"Connection timed out. {ex.Message}", 1));
    }
    catch (OperationCanceledException)
    {
      return (false, new ErrorResult("Cancelled", 1));
    }
  }
}
