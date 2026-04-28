using Slice.Common.Models;

namespace Agent.Services;

internal static class SystemdOutputParser
{
    internal static ServiceStatus ParseServiceStatus(string rawOutput)
    {
        var dict = rawOutput
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(),
                          StringComparer.OrdinalIgnoreCase);

        return new ServiceStatus
        {
            Id                   = dict.GetValueOrDefault("Id") ?? "",
            Description          = dict.GetValueOrDefault("Description") ?? "",
            LoadState            = dict.GetValueOrDefault("LoadState") ?? "",
            ActiveState          = dict.GetValueOrDefault("ActiveState") ?? "",
            SubState             = dict.GetValueOrDefault("SubState") ?? "",
            StateChangeTimestamp = dict.GetValueOrDefault("StateChangeTimestamp") ?? "",
            MainPid              = int.TryParse(dict.GetValueOrDefault("MainPID"), out var pid) ? pid : 0,
            MemoryCurrent        = ParseUlong(dict.GetValueOrDefault("MemoryCurrent")),
            MemoryPeak           = ParseUlong(dict.GetValueOrDefault("MemoryPeak")),
            CpuUsageNSec         = ParseUlong(dict.GetValueOrDefault("CPUUsageNSec")),
            Result               = dict.GetValueOrDefault("Result") ?? "",
        };
    }

    private static ulong ParseUlong(string? value) =>
        ulong.TryParse(value, out var result) && result != ulong.MaxValue ? result : 0;
}
