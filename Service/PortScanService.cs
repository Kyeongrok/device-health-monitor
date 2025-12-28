using System.Collections.Concurrent;
using System.Net.Sockets;

namespace DHM.Service;

public class PortScanService
{
    public int Timeout { get; set; } = 100;
    public int ThrottleLimit { get; set; } = 50;

    public event Action<int, int>? OnProgress; // scanned, total
    public event Action<ConcurrentDictionary<int, bool>>? OnComplete;

    public async Task ScanAsync(string targetIp, int startPort, int endPort)
    {
        var portStatus = new ConcurrentDictionary<int, bool>();
        var totalPorts = endPort - startPort + 1;
        var scannedCount = 0;

        var semaphore = new SemaphoreSlim(ThrottleLimit);
        var tasks = new List<Task>();

        for (int port = startPort; port <= endPort; port++)
        {
            int p = port;
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    using var tcp = new TcpClient();
                    var connectTask = tcp.ConnectAsync(targetIp, p);
                    var completed = await Task.WhenAny(connectTask, Task.Delay(Timeout)) == connectTask;

                    if (completed && tcp.Connected)
                    {
                        portStatus[p] = true;
                    }
                }
                catch { }
                finally
                {
                    semaphore.Release();
                    var count = Interlocked.Increment(ref scannedCount);
                    OnProgress?.Invoke(count, totalPorts);
                }
            }));
        }

        await Task.WhenAll(tasks);
        OnComplete?.Invoke(portStatus);
    }

    public static string GetPortName(int port) => port switch
    {
        20 => "FTP-DATA",
        21 => "FTP",
        22 => "SSH",
        23 => "Telnet",
        25 => "SMTP",
        53 => "DNS",
        80 => "HTTP",
        110 => "POP3",
        143 => "IMAP",
        443 => "HTTPS",
        445 => "SMB",
        993 => "IMAPS",
        995 => "POP3S",
        1433 => "MSSQL",
        3306 => "MySQL",
        3389 => "RDP",
        5432 => "PostgreSQL",
        6379 => "Redis",
        8080 => "HTTP-Alt",
        8443 => "HTTPS-Alt",
        27017 => "MongoDB",
        _ => ""
    };
}
