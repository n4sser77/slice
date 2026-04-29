using System.CommandLine;
using Agent.Cli.Core;
using Agent.Cli.Core.Events;
using Agent.Cli.Presentation;
using Agent.Cli.Core.Results;
using System.Runtime.CompilerServices;
using System.Net;

namespace Agent.Cli.Commands;

public class StopServiceCommand(string serviceName, HttpClient httpClient) : ICommand
{
  public static void Register(RootCommand root, HttpClient httpClient)
  {
    Command command = new("stop", "Stops a running service.");
    Argument<string> serviceNameArg = new Argument<string>("service-name")
    {
      Description = "The name of the service without the 'slice-' prefix and '.service' suffix."
    };
    command.Add(serviceNameArg);

    command.SetAction(async (parseResult, ct) =>
    {
      var raw = parseResult.GetValue(serviceNameArg)!;
      var name = raw.EndsWith(".service", StringComparison.OrdinalIgnoreCase) ? raw[..^".service".Length] : raw;
      var cmd = new StopServiceCommand(name, httpClient);
      return await ConsoleRenderer.RenderAsync(cmd.ExecuteStreamingAsync(ct), ct);
    });

    root.Subcommands.Add(command);

  }

  public async IAsyncEnumerable<ExecutionEvent> ExecuteStreamingAsync(
    [EnumeratorCancellation] CancellationToken ct = default)
  {
    var (_, err) = await TryStopService(ct);
    if (err is ErrorResult errorResult)
    {
      yield return new FinalResult(errorResult);
      yield break;
    }

    yield return new FinalResult(new SuccessResult($"Stopped {serviceName}"));
  }

  private async Task<(bool stopped, ErrorResult? err)> TryStopService(CancellationToken ct)
  {
    try
    {
      var response = await httpClient.PostAsync($"services/{serviceName}/stop", null, ct);

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
