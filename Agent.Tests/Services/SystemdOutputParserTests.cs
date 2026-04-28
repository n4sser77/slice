using Agent.Services;
using Slice.Common.Models;

namespace Agent.Tests.Services;

public class SystemdOutputParserTests
{
    [Fact]
    public void ParseServiceStatus_AllFieldsPresent_MapsCorrectly()
    {
        var raw = """
            Id=slice-agent.service
            Description=Uploaded C# Service: slice-agent
            LoadState=loaded
            ActiveState=active
            SubState=running
            StateChangeTimestamp=Wed 2026-04-22 00:16:42 CEST
            MainPID=64202
            Result=success
            MemoryCurrent=21827584
            MemoryPeak=23379968
            CPUUsageNSec=2569266000
            """;

        var result = SystemdOutputParser.ParseServiceStatus(raw);

        Assert.Equal("slice-agent.service", result.Id);
        Assert.Equal("Uploaded C# Service: slice-agent", result.Description);
        Assert.Equal("loaded", result.LoadState);
        Assert.Equal("active", result.ActiveState);
        Assert.Equal("running", result.SubState);
        Assert.Equal("Wed 2026-04-22 00:16:42 CEST", result.StateChangeTimestamp);
        Assert.Equal(64202, result.MainPid);
        Assert.Equal(21827584UL, result.MemoryCurrent);
        Assert.Equal(23379968UL, result.MemoryPeak);
        Assert.Equal(2569266000UL, result.CpuUsageNSec);
        Assert.Equal("success", result.Result);
    }

    [Fact]
    public void ParseServiceStatus_ValueContainsEquals_PreservesFullValue()
    {
        var raw = "Description=My App = Version 2";

        var result = SystemdOutputParser.ParseServiceStatus(raw);

        Assert.Equal("My App = Version 2", result.Description);
    }

    [Fact]
    public void ParseServiceStatus_ServiceInactive_MapsCorrectly()
    {
        var raw = """
            ActiveState=inactive
            SubState=dead
            MainPID=0
            Result=success
            """;

        var result = SystemdOutputParser.ParseServiceStatus(raw);

        Assert.Equal("inactive", result.ActiveState);
        Assert.Equal("dead", result.SubState);
        Assert.Equal(0, result.MainPid);
        Assert.Equal("success", result.Result);
    }

    [Fact]
    public void ParseServiceStatus_ServiceFailed_MapsCorrectly()
    {
        var raw = """
            ActiveState=failed
            SubState=failed
            Result=exit-code
            """;

        var result = SystemdOutputParser.ParseServiceStatus(raw);

        Assert.Equal("failed", result.ActiveState);
        Assert.Equal("failed", result.SubState);
        Assert.Equal("exit-code", result.Result);
    }

    [Fact]
    public void ParseServiceStatus_MemoryUnavailableSentinel_MapsToZero()
    {
        // systemd emits ulong.MaxValue when memory accounting is unavailable
        var raw = "MemoryCurrent=18446744073709551615";

        var result = SystemdOutputParser.ParseServiceStatus(raw);

        Assert.Equal(0UL, result.MemoryCurrent);
    }

    [Fact]
    public void ParseServiceStatus_MissingKeys_DefaultsWithoutThrowing()
    {
        var raw = """
            Id=slice-agent.service
            ActiveState=active
            """;

        var result = SystemdOutputParser.ParseServiceStatus(raw);

        Assert.Equal("slice-agent.service", result.Id);
        Assert.Equal("active", result.ActiveState);
        Assert.Equal("", result.Description);
        Assert.Equal(0, result.MainPid);
        Assert.Equal(0UL, result.MemoryCurrent);
    }

    [Fact]
    public void ParseServiceStatus_EmptyInput_ReturnsDefaults()
    {
        var result = SystemdOutputParser.ParseServiceStatus("");

        Assert.Equal("", result.Id);
        Assert.Equal("", result.ActiveState);
        Assert.Equal(0, result.MainPid);
        Assert.Equal(0UL, result.MemoryCurrent);
    }

    [Fact]
    public void ParseServiceStatus_LinesWithoutEquals_AreIgnored()
    {
        var raw = """
            this line has no equals sign
            Id=slice-agent.service
            another bad line
            """;

        var result = SystemdOutputParser.ParseServiceStatus(raw);

        Assert.Equal("slice-agent.service", result.Id);
    }
}
