namespace Slice.Common.Models;

public class ServiceStatus
{
  public string Id { get; set; } = "";
  public string Unit { get; set; } = "";
  public string Description { get; set; } = "";
  public string LoadState { get; set; } = "";
  public string ActiveState { get; set; } = "";
  public string SubState { get; set; } = "";
  public int MainPid { get; set; }
  public ulong MemoryCurrent { get; set; }
  public ulong MemoryPeak { get; set; }
  public ulong CpuUsageNSec { get; set; }
  public string StateChangeTimestamp { get; set; } = "";
  public string Result { get; set; } = "";
}

// systemctl --user show slice - agent.service
//  --property=id,description,loadstate,activestate,substate,statechangetimestamp,mainpid,memorycurrent,memorypeak,cpuusagensec,result
//  id = slice - agent.service
//   description=uploaded c# service: slice-agent
//   loadstate=loaded
//   activestate = active
//   substate=running
//   statechangetimestamp = wed 2026-04-22 00:16:42 cest
//   mainpid = 64202
//   result=success
//   memorycurrent = 21827584
//   memorypeak=23379968
//   cpuusagensec=2569266000
