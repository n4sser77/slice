namespace Agent.Cli;

public sealed record CliConfig(Uri BaseAddress)
{
    public static readonly CliConfig Default =
        new(new Uri("http://localhost:5165/v1/"));
}
