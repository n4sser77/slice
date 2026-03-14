namespace Agent.Models;

public class SystemdService
{
    public string Unit { get; set; } = "";
    public string Loaded { get; set; } = "";
    public string Active { get; set; } = "";
    public string Sub { get; set; } = "";
    public string Description { get; set; } = "";
}
