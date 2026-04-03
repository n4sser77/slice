using Agent.Cli.Core.Results;

namespace Agent.Cli.Core.Events;

public abstract record ExecutionEvent;

public sealed record StepStarted(string Name) : ExecutionEvent;

public sealed record StepCompleted(string Name, TimeSpan Duration) : ExecutionEvent;

public sealed record StepFailed(string Name, string Error) : ExecutionEvent;

public sealed record StatusMessage(string Message) : ExecutionEvent;

public sealed record ProgressUpdate(double Percentage, string Message) : ExecutionEvent;

public sealed record FinalResult(CommandResult Result) : ExecutionEvent;
public sealed record DebugEvent(string Message) : ExecutionEvent;
