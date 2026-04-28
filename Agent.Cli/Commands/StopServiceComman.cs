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
    Argument<string> serivceNameArg = new Argument<string>("service-name")
    {
      Description = "The name of the service including prefix."
    };
    command.Add(serivceNameArg);

    command.SetAction(async (parseResult, ct) =>
    {
      var cmd = new StopServiceCommand(parseResult.GetValue(serivceNameArg)!, httpClient);
      return await ConsoleRenderer.RenderAsync(cmd.ExecuteStreamingAsync(ct), ct);
    });

    root.Subcommands.Add(command);

  }

  public async IAsyncEnumerable<ExecutionEvent> ExecuteStreamingAsync(
    [EnumeratorCancellation] CancellationToken ct = default)
  {
    var (stopped, err) = await TryStopService(ct);
    if (err is ErrorResult errorResult)
    {
      yield return new FinalResult(errorResult);
      yield break;
    }

    yield return new FinalResult(new SuccessResult($"Stopped {serviceName}"));
  }

  private async Task<(bool stopped, ErrorResult? err)> TryStopService(CancellationToken ct)
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
}
