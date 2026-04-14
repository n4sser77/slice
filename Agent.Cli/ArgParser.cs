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

        if (args.Length == 2 && args[0] == "deploy")
            return new DeployServiceCommand(args[1]);

        if (args.Length == 1 && args[0] == "list")
            return new GetServicesCommand();

        return null;
    }
}
