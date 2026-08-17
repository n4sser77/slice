using System.Net.Http.Headers;

namespace Slice.Cli.Auth;

public class ApiKeyHandler : DelegatingHandler
{
  private const string ApiKeyEnvVarName = "SLICE_API_KEY";
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
  {
    var key = Environment.GetEnvironmentVariable(ApiKeyEnvVarName);
    if (!string.IsNullOrEmpty(key))
    {
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }
    return base.SendAsync(request, ct);
  }
}


