namespace Notes.Services;

public interface IPortService
{
    Task<List<PortUsage>> GetPortUsageAsync(int? port = null);
    Task<List<PortUsage>> GetListeningPortsAsync();
    Task<bool> KillProcessAsync(int pid);
}

public class PortUsage
{
    public int Port { get; set; }
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = "";
    public string? CommandLine { get; set; }
    public string Protocol { get; set; } = "TCP";
    public string State { get; set; } = "";
    public string LocalAddress { get; set; } = "";
    public string RemoteAddress { get; set; } = "";
}
