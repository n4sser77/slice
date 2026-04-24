using System.Net;
using System.Text;
using System.Text.Json;
using Agent.Cli.Commands;
using Agent.Cli.Core.Events;
using Agent.Cli.Core.Results;
using Agent.Cli.Presentation;
using Agent.Cli.Serialization;
using Slice.Common.Models;

namespace Agent.Cli.Tests;

public class GetServicesCommandTests
{
  [Fact]
  public async Task ExecuteStreamingAsync_EmitsServicesListedEvent()
  {
    var payload = JsonSerializer.Serialize(
    [
        new SystemdService { Unit = "slice-b.service", Active = "active", Sub = "running", Description = "B service" },
            new SystemdService { Unit = "slice-a.service", Active = "inactive", Sub = "dead", Description = "A service" }
    ], CliJsonContext.Default.ListSystemdService);

    using var client = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StringContent(payload, Encoding.UTF8, "application/json")
    });

    var sut = new GetServicesCommand(client);

    var events = await ReadEvents(sut);
    var listed = Assert.IsType<ServicesListed>(events[0]);
    Assert.Collection(listed.Services,
        s => Assert.Equal("slice-b.service", s.Unit),
        s => Assert.Equal("slice-a.service", s.Unit));

    var final = Assert.IsType<FinalResult>(events[1]);
    var success = Assert.IsType<SuccessResult>(final.Result);
    Assert.Equal("Listed services.", success.Message);
  }

  [Fact]
  public async Task ExecuteStreamingAsync_ReturnsErrorForUpstreamFailure()
  {
    using var client = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.BadGateway)
    {
      ReasonPhrase = "Bad Gateway",
      Content = new StringContent("systemctl unavailable", Encoding.UTF8, "text/plain")
    });

    var sut = new GetServicesCommand(client);

    var final = await ReadFinalResult(sut);
    var error = Assert.IsType<ErrorResult>(final.Result);
    Assert.Contains("Error with upstream", error.Message);
  }

  [Fact]
  public async Task ExecuteStreamingAsync_ReturnsErrorForInvalidJson()
  {
    using var client = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StringContent("{\"unexpected\":true}", Encoding.UTF8, "application/json")
    });

    var sut = new GetServicesCommand(client);

    var final = await ReadFinalResult(sut);
    var error = Assert.IsType<ErrorResult>(final.Result);
    Assert.Contains("Invalid response JSON", error.Message);
  }

  [Fact]
  public async Task ConsoleRenderer_RendersServicesAsTable()
  {
    var events = GetEvents(
        new ServicesListed(
        [
            new SystemdService { Unit = "slice-b.service", Active = "active", Sub = "running", Description = "B service" },
                new SystemdService { Unit = "slice-a.service", Active = "inactive", Sub = "dead", Description = "A service" }
        ]),
        new FinalResult(new SuccessResult("Listed services."))
    );

    var originalOut = Console.Out;
    var output = new StringWriter();
    Console.SetOut(output);
    try
    {
      _ = await ConsoleRenderer.RenderAsync(events);
    }
    finally
    {
      Console.SetOut(originalOut);
    }

    var rendered = output.ToString();
    Assert.Contains("UNIT", rendered);
    Assert.Contains("slice-a.service", rendered);
    Assert.Contains("slice-b.service", rendered);
  }

  private static HttpClient CreateHttpClient(HttpResponseMessage response)
  {
    var handler = new StubHttpMessageHandler(_ => response);
    return new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5165/v1/") };
  }

  private static async Task<FinalResult> ReadFinalResult(GetServicesCommand command)
  {
    await foreach (var evt in command.ExecuteStreamingAsync())
    {
      if (evt is FinalResult final)
        return final;
    }

    throw new InvalidOperationException("Command produced no FinalResult event.");
  }

  private static async Task<List<ExecutionEvent>> ReadEvents(GetServicesCommand command)
  {
    var events = new List<ExecutionEvent>();
    await foreach (var evt in command.ExecuteStreamingAsync())
    {
      events.Add(evt);
    }
    return events;
  }

  private static async IAsyncEnumerable<ExecutionEvent> GetEvents(params ExecutionEvent[] events)
  {
    foreach (var evt in events)
    {
      yield return evt;
      await Task.Yield();
    }
  }

  private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(responder(request));
  }
}
