namespace Agent.Configuration;

public class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";
    public string AdminUrl { get; set; } = "http://localhost:2019";
    public string BaseDomain { get; set; } = string.Empty;
}
