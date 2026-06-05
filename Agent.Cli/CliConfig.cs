namespace Agent.Cli;

public sealed record CliConfig(Uri BaseAddress, string TargetHost)
{
  public static readonly CliConfig Default =
      new(

          BaseAddress: new Uri(Environment.GetEnvironmentVariable("SLICE_AGENT_URL") is { } url
          ? url.TrimEnd('/') + "/v1/"
          : "http://localhost:5165/v1/"),

          TargetHost: Environment.GetEnvironmentVariable("SLICE_AGENT_TARGET_HOST") is { } host
          ? host.Trim()
          : "linux-arm64"
          );
}
