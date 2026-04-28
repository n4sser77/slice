using System.CommandLine;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Agent.Cli.Core;
using Agent.Cli.Core.Events;
using Agent.Cli.Core.Results;
using Agent.Cli.Presentation;
using Agent.Cli.Serialization;
using Slice.Common.Models;

namespace Agent.Cli.Commands;

public class GetServiceStatusCommand(string serviceName, HttpClient httpClient) : ICommand
{
  public static void Register(RootCommand root, HttpClient httpClient)
  {
    var command = new Command("status", "Inspect the status of a deployed service");
    var serviceNameArg = new Argument<string>("service-name")
    {
      Description = "The name of the service without the 'slice-' prefix and '.service' suffix."
    };

    command.Add(serviceNameArg);

    command.SetAction(async (parseResult, ct) =>
    {
      var cmd = new GetServiceStatusCommand(parseResult.GetValue(serviceNameArg)!, httpClient);
      return await ConsoleRenderer.RenderAsync(cmd.ExecuteStreamingAsync(ct), ct);
    });

    root.Subcommands.Add(command);
  }

  public async IAsyncEnumerable<ExecutionEvent> ExecuteStreamingAsync(
      [EnumeratorCancellation] CancellationToken ct = default)
  {
    var (status, err) = await TryGetServiceStatus(ct);
    if (err is ErrorResult errorResult)
    {
      yield return new FinalResult(errorResult);
      yield break;
    }

    yield return new ServiceStatusReceived(status!);
    yield return new FinalResult(new SuccessResult($"Status for {serviceName}."));
  }

  private async Task<(ServiceStatus? status, ErrorResult? error)> TryGetServiceStatus(CancellationToken ct)
  {
    try
    {
      var response = await httpClient.GetAsync($"services/{serviceName}", ct);

      if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        return (null, new ErrorResult($"Service '{serviceName}' not found.", 1));

      if (!response.IsSuccessStatusCode)
      {
        var detail = await response.Content.ReadAsStringAsync(ct);
        return (null, new ErrorResult($"Error from server: {response.StatusCode}. {detail}", 1));
      }

      var status = await response.Content.ReadFromJsonAsync(CliJsonContext.Default.ServiceStatus, ct);
      if (status is null)
        return (null, new ErrorResult("Invalid response payload.", 1));

      return (status, null);
    }
    catch (HttpRequestException ex)
    {
      return (null, new ErrorResult($"Connection failed: {ex.Message}. Make sure the deployment service is running.", 1));
    }
    catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
    {
      return (null, new ErrorResult($"Connection timed out. {ex.Message}", 1));
    }
    catch (OperationCanceledException)
    {
      return (null, new ErrorResult("Cancelled", 1));
    }
    catch (System.Text.Json.JsonException ex)
    {
      return (null, new ErrorResult($"Invalid response JSON: {ex.Message}", 1));
    }
  }
}
