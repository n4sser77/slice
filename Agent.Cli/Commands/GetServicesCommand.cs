using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Agent.Cli.Core;
using Agent.Cli.Core.Events;
using Agent.Cli.Core.Results;
using Agent.Cli.Serialization;
using Slice.Common.Models;

namespace Agent.Cli.Commands;

public class GetServicesCommand : ICommand
{

    public async IAsyncEnumerable<ExecutionEvent> ExecuteStreamingAsync(
            [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (res, err) = await TryGetServices(ct);
        if (err is ErrorResult errorResult)
        {
            yield return new FinalResult(errorResult);
            yield break;
        }

        yield return new FinalResult(new SuccessResult(JsonSerializer.Serialize(res, CliJsonContext.Default.ListSystemdService)));

    }

    private async Task<(List<SystemdService>? services, ErrorResult? error)> TryGetServices(CancellationToken ct = default)
    {
        try
        {
            using var client = new HttpClient
            {
                BaseAddress = new("http://localhost:5165/v1/"),
                Timeout = TimeSpan.FromSeconds(15)
            };
            var getServicesResults = await client.GetAsync("services");

            if (!getServicesResults.IsSuccessStatusCode)
            {
                var errorContent = await getServicesResults.Content.ReadAsStringAsync();
                return (null, new ErrorResult($"Error with upstream: {getServicesResults.StatusCode}. Details: {errorContent} - {getServicesResults.ReasonPhrase}"));
            }

            var content = await getServicesResults.Content.ReadFromJsonAsync(CliJsonContext.Default.ListSystemdService, ct);
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
        catch (Exception ex)
        {
            return (null, new ErrorResult(ex.Message, 1));
        }
    }
}

