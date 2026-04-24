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

public class GetServicesCommand(HttpClient httpClient) : ICommand
{
    public static void Register(RootCommand root, HttpClient httpClient)
    {
        var command = new Command("list", "List all deployed services");

        command.SetAction(async (parseResult, ct) =>
        {
            var cmd = new GetServicesCommand(httpClient);
            return await ConsoleRenderer.RenderAsync(cmd.ExecuteStreamingAsync(ct), ct);
        });

        root.Subcommands.Add(command);
    }

    public async IAsyncEnumerable<ExecutionEvent> ExecuteStreamingAsync(
            [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (res, err) = await TryGetServices(ct);
        if (err is ErrorResult errorResult)
        {
            yield return new FinalResult(errorResult);
            yield break;
        }

        yield return new ServicesListed(res!);
        yield return new FinalResult(new SuccessResult("Listed services."));
    }

    private async Task<(List<SystemdService>? services, ErrorResult? error)> TryGetServices(CancellationToken ct = default)
    {
        try
        {
            var getServicesResults = await httpClient.GetAsync("services", ct);

            if (!getServicesResults.IsSuccessStatusCode)
            {
                var errorContent = await getServicesResults.Content.ReadAsStringAsync();
                var msg = $"Error with upstream: {getServicesResults.StatusCode}. Details: {errorContent} - {getServicesResults.ReasonPhrase}";
                return (null, new ErrorResult(msg));
            }

            var content = await getServicesResults.Content.ReadFromJsonAsync(CliJsonContext.Default.ListSystemdService, ct);
            if (content is null)
            {
                return (null, new ErrorResult("Invalid response payload: expected a JSON array of services.", 1));
            }

            return (content, null);

        }
        catch (HttpRequestException ex)
        {
            return (null, new ErrorResult($"Connection failed: {ex.Message}. Make sure the deployment service is running.", 1));
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            return (null, new ErrorResult($"Connection timed out. Is the deployment service running at http://localhost:5165? {ex.Message}", 1));
        }
        catch (OperationCanceledException)
        {
            return (null, new ErrorResult("Cancelled", 1));
        }
        catch (NotSupportedException ex)
        {
            return (null, new ErrorResult($"Invalid response content type: {ex.Message}", 1));
        }
        catch (System.Text.Json.JsonException ex)
        {
            return (null, new ErrorResult($"Invalid response JSON: {ex.Message}", 1));
        }
    }
}
