using Agent.Cli.Commands;
using Agent.Cli.Core;
using Agent.Cli.Presentation;

ArgParser parser = new();

ICommand? command = parser.ParseArgs(args);

if (command is null)
    return 1;

var events = command.ExecuteStreamingAsync();
int exitCode = await ConsoleRenderer.RenderAsync(events);

return exitCode;