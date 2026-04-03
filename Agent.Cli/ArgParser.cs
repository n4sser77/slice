using Agent.Cli.Core;

namespace Agent.Cli.Commands;

public class ArgParser
{
    public ICommand? ParseArgs(string[] args)
    {
        if (args.Contains("-h") || args.Contains("--help"))
            return new HelpCommand();

        if (args.Length == 0)
            return null;

        if (args.Length == 1)
            return new DeployServiceCommand(args[0]);

        return null;
    }
}
