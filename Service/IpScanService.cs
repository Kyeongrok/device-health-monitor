using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DHM.Service;

public class IpScanService
{
    public int Timeout { get; set; } = 1000;
    public int ThrottleLimit { get; set; } = 50;

    public event Action<int, int>? OnProgress; // scanned, total
    public event Action<HostInfo>? OnHostFound; // 호스트 발견 시 실시간 알림
    public event Action<List<HostInfo>>? OnComplete;

    public async Task ScanSubnetAsync(string baseIp, int startHost = 1, int endHost = 254)
    {
        var hosts = new ConcurrentBag<HostInfo>();
        var totalHosts = endHost - startHost + 1;
        var scannedCount = 0;

        // baseIp에서 네트워크 부분 추출 (예: 192.168.1.100 -> 192.168.1)
        var ipParts = baseIp.Split('.');
        if (ipParts.Length != 4) return;

        var networkPrefix = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}";

        var semaphore = new SemaphoreSlim(ThrottleLimit);
        var tasks = new List<Task>();

        for (int i = startHost; i <= endHost; i++)
        {
            var hostNum = i;
            var targetIp = $"{networkPrefix}.{hostNum}";

            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var hostInfo = await ScanHostAsync(targetIp);
                    if (hostInfo.IsAlive)
                    {
                        hosts.Add(hostInfo);
                        OnHostFound?.Invoke(hostInfo); // 실시간 알림
                    }
                }
                finally
                {
                    semaphore.Release();
                    var count = Interlocked.Increment(ref scannedCount);
                    OnProgress?.Invoke(count, totalHosts);
                }
            }));
        }

        await Task.WhenAll(tasks);

        var sortedHosts = hosts.OrderBy(h => GetIpSortKey(h.IpAddress)).ToList();
        OnComplete?.Invoke(sortedHosts);
    }

    private async Task<HostInfo> ScanHostAsync(string ip)
    {
        var hostInfo = new HostInfo { IpAddress = ip };

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ip, Timeout);

            if (reply.Status == IPStatus.Success)
            {
                hostInfo.IsAlive = true;
                hostInfo.ResponseTime = (int)reply.RoundtripTime;

                // 호스트명 조회 시도
                try
                {
                    var hostEntry = await Dns.GetHostEntryAsync(ip);
                    hostInfo.Hostname = hostEntry.HostName;
                }
                catch { }

                // 주요 포트 빠르게 확인
                hostInfo.OpenPorts = await QuickPortScanAsync(ip);

                // MAC 주소 조회 (같은 서브넷만)
                hostInfo.MacAddress = GetMacAddress(ip);
            }
        }
        catch { }

        return hostInfo;
    }

    private async Task<List<int>> QuickPortScanAsync(string ip)
    {
        var openPorts = new ConcurrentBag<int>();
        var commonPorts = new[] { 22, 23, 80, 443, 445, 3389, 8080 };

        var tasks = commonPorts.Select(async port =>
        {
            try
            {
                using var tcp = new TcpClient();
                var connectTask = tcp.ConnectAsync(ip, port);
                if (await Task.WhenAny(connectTask, Task.Delay(200)) == connectTask && tcp.Connected)
                {
                    openPorts.Add(port);
                }
            }
            catch { }
        });

        await Task.WhenAll(tasks);
        return openPorts.OrderBy(p => p).ToList();
    }

    private string? GetMacAddress(string ip)
    {
        // ARP 테이블에서 MAC 주소 조회 시도
        try
        {
            var arp = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "arp",
                Arguments = ip,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(arp);
            if (process == null) return null;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // MAC 주소 패턴 찾기 (xx:xx:xx:xx:xx:xx 또는 xx-xx-xx-xx-xx-xx)
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains(ip))
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        if (part.Contains(':') && part.Length >= 17)
                        {
                            return part.ToUpper();
                        }
                        if (part.Contains('-') && part.Length >= 17)
                        {
                            return part.Replace('-', ':').ToUpper();
                        }
                    }
                }
            }
        }
        catch { }

        return null;
    }

    private long GetIpSortKey(string ip)
    {
        var parts = ip.Split('.');
        if (parts.Length != 4) return 0;

        return long.Parse(parts[0]) * 16777216L +
               long.Parse(parts[1]) * 65536L +
               long.Parse(parts[2]) * 256L +
               long.Parse(parts[3]);
    }

    public static string GetNetworkPrefix(string ip)
    {
        var parts = ip.Split('.');
        if (parts.Length != 4) return ip;
        return $"{parts[0]}.{parts[1]}.{parts[2]}";
    }
}

public class HostInfo
{
    public string IpAddress { get; set; } = "";
    public string? Hostname { get; set; }
    public string? MacAddress { get; set; }
    public bool IsAlive { get; set; }
    public int ResponseTime { get; set; }
    public List<int> OpenPorts { get; set; } = new();

    public string GetPortsDisplay()
    {
        if (OpenPorts.Count == 0) return "";
        return string.Join(",", OpenPorts.Select(p => PortScanService.GetPortName(p) != ""
            ? $"{p}({PortScanService.GetPortName(p)})"
            : p.ToString()));
    }
}
