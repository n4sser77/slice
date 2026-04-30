namespace Agent.Cli;

public sealed record CliConfig(Uri BaseAddress)
{
    public static readonly CliConfig Default =
        new(new Uri(Environment.GetEnvironmentVariable("SLICE_AGENT_URL") is { } url
            ? url.TrimEnd('/') + "/v1/"
            : "http://localhost:5165/v1/"));
}
