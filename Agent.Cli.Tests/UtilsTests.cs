namespace Agent.Cli.Tests;

public class UtilsTests
{

    [Fact]
    public void FindPublishPath_Returns_correct_path()
    {
        const string stdout = @"
    Detail
      | Determining projects to restore...
      | Restored /home/qanasser/projects/slice/Agent.Cli/Agen
    t.Cli.csproj (in 942 ms).
      | Agent.Cli -> /home/qanasser/projects/slice/Agent.Cli/
    bin/Release/net10.0/linux-arm64/Agent.Cli.dll
      | Agent.Cli -> /home/qanasser/projects/slice/Agent.Cli/
    bin/Release/net10.0/linux-arm64/publish/

";
        var expected = Path.GetFullPath(
              "/home/qanasser/projects/slice/Agent.Cli/" +
              "bin/Release/net10.0/linux-arm64/publish/".Trim());

        var path = Agent.Cli.Utils.PathFinder.FindPublishPath(stdout);
        Console.WriteLine("path return: " + path);
        Assert.Equal(expected, path);
    }
}
