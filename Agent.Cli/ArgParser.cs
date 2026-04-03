using Agent.Cli.Core;
using Agent.Cli.Commands;

namespace Agent.Cli.Commands;

public class ArgParser
{
    public ICommand? ParseArgs(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("Error: Provide a target app for deployment");
            return null;
        }

        if (args.Length == 1)
        {
            return new DeployServiceCommand(args[0]);
        }

        return null;
    }
}

