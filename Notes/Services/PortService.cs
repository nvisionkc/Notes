using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Notes.Services;

public class PortService : IPortService
{
    public async Task<List<PortUsage>> GetPortUsageAsync(int? port = null)
    {
        var results = new List<PortUsage>();

#if WINDOWS
        try
        {
            // Run netstat -ano to get all connections with PIDs
            var netstatOutput = await RunCommandAsync("netstat", "-ano");
            var lines = netstatOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Get process info cache
            var processCache = new Dictionary<int, (string Name, string? CommandLine)>();

            foreach (var line in lines.Skip(4)) // Skip header lines
            {
                var parsed = ParseNetstatLine(line);
                if (parsed == null) continue;

                // Filter by port if specified
                if (port.HasValue && parsed.Port != port.Value) continue;

                // Get process info
                if (!processCache.TryGetValue(parsed.ProcessId, out var processInfo))
                {
                    processInfo = GetProcessInfo(parsed.ProcessId);
                    processCache[parsed.ProcessId] = processInfo;
                }

                parsed.ProcessName = processInfo.Name;
                parsed.CommandLine = processInfo.CommandLine;

                results.Add(parsed);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting port usage: {ex.Message}");
        }
#endif

        return results.OrderBy(p => p.Port).ThenBy(p => p.ProcessId).ToList();
    }

    public async Task<List<PortUsage>> GetListeningPortsAsync()
    {
        var all = await GetPortUsageAsync();
        return all.Where(p => p.State == "LISTENING").ToList();
    }

    public Task<bool> KillProcessAsync(int pid)
    {
#if WINDOWS
        try
        {
            var process = Process.GetProcessById(pid);
            process.Kill();
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error killing process {pid}: {ex.Message}");
            return Task.FromResult(false);
        }
#else
        return Task.FromResult(false);
#endif
    }

#if WINDOWS
    private static async Task<string> RunCommandAsync(string command, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }

    private static PortUsage? ParseNetstatLine(string line)
    {
        // Example lines:
        // TCP    0.0.0.0:135            0.0.0.0:0              LISTENING       1234
        // TCP    127.0.0.1:5000         127.0.0.1:54321        ESTABLISHED     5678
        // UDP    0.0.0.0:5353           *:*                                    1234

        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;

        var parts = Regex.Split(trimmed, @"\s+");
        if (parts.Length < 4) return null;

        var protocol = parts[0].ToUpperInvariant();
        if (protocol != "TCP" && protocol != "UDP") return null;

        var localAddress = parts[1];
        var remoteAddress = parts[2];

        // Extract port from local address
        var lastColon = localAddress.LastIndexOf(':');
        if (lastColon < 0) return null;

        if (!int.TryParse(localAddress.Substring(lastColon + 1), out var port)) return null;

        // State and PID depend on protocol
        string state;
        int pid;

        if (protocol == "TCP")
        {
            if (parts.Length < 5) return null;
            state = parts[3];
            if (!int.TryParse(parts[4], out pid)) return null;
        }
        else // UDP
        {
            state = "";
            if (!int.TryParse(parts[3], out pid)) return null;
        }

        return new PortUsage
        {
            Protocol = protocol,
            LocalAddress = localAddress,
            RemoteAddress = remoteAddress,
            Port = port,
            State = state,
            ProcessId = pid
        };
    }

    private static (string Name, string? CommandLine) GetProcessInfo(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            return (process.ProcessName, null); // CommandLine requires elevated permissions
        }
        catch
        {
            return ($"PID {pid}", null);
        }
    }
#endif
}
