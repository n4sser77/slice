using System.Runtime.CompilerServices;
using Agent.Cli.Core;
using Agent.Cli.Core.Events;
using Agent.Cli.Core.Results;
using Agent.Cli.Help;

namespace Agent.Cli.Commands;

public class HelpCommand : ICommand
{
    public async IAsyncEnumerable<ExecutionEvent> ExecuteStreamingAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        HelpDisplay.Show();
        yield return new FinalResult(new SuccessResult(""));
    }
}
