namespace Agent.Cli.Core.Results;

public abstract record CommandResult;

public sealed record SuccessResult(string Message, string? FilePath = null) : CommandResult;

public sealed record ErrorResult(string Message, int ExitCode = 1) : CommandResult;
