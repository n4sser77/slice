using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Agent.Auth;

public class ApiKeyAuthHandler(
    IOptionsMonitor<ApiKeyAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
  : AuthenticationHandler<ApiKeyAuthOptions>(options, logger, encoder)
{
  private const string ApiKeyEnvVarName = "SLICE_API_KEY";
  protected override Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    string? expectedKey = Environment.GetEnvironmentVariable(ApiKeyEnvVarName);

    if (string.IsNullOrEmpty(expectedKey))
    {
      return Task.FromResult(
          AuthenticateResult.Fail("API key authentication is not configured."));
    }
    if (!AuthHeaderExists(out var value))
    {
      return Task.FromResult(AuthenticateResult.NoResult());
    }

    string providedKey = value.ToString()["Bearer ".Length..].Trim();

    if (!KeyValid(expectedKey, providedKey))
    {
      return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
    }


    List<Claim> claims = [new Claim(ClaimTypes.Name, "cli"), new Claim(ClaimTypes.Role, "deployer")];
    ClaimsIdentity identity = new(claims, Scheme.Name);
    AuthenticationTicket ticket = new(new ClaimsPrincipal(identity), Scheme.Name);

    return Task.FromResult(AuthenticateResult.Success(ticket));
  }
  private bool AuthHeaderExists(out StringValues value)
  {
    bool authHeaderExists = Request.Headers.TryGetValue("Authorization", out value);
    bool startsCorrect = value.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
    return (authHeaderExists && startsCorrect);
  }
  private bool KeyValid(string? expectedKey, string providedKey)
  {
    if (string.IsNullOrEmpty(expectedKey))
    {
      return false;
    }

    bool providedKeyIsValid = CryptographicOperations
      .FixedTimeEquals(Encoding.UTF8.GetBytes(providedKey),
                       Encoding.UTF8.GetBytes(expectedKey));

    return providedKeyIsValid;
  }
}

public class ApiKeyAuthOptions : AuthenticationSchemeOptions
{
  public const string SchemeName = "ApiKey";
}
