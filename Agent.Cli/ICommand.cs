using System.Runtime.CompilerServices;
using Agent.Cli.Core.Events;

namespace Agent.Cli.Core;

public interface ICommand
{
    IAsyncEnumerable<ExecutionEvent> ExecuteStreamingAsync(CancellationToken ct = default);
}