using Agent.Cli.Commands;

ArgParser parser = new();

ICommand? command = parser.ParseArgs(args);

if (command is null)
{
    return 1;
}

int exitCode = await command.Execute();

return exitCode;


