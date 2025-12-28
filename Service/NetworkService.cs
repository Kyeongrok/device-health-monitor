using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace DHM.Service;

public static class NetworkService
{
    public static List<(string Name, string IP, string Subnet)> GetLocalNetworkInfo()
    {
        var result = new List<(string, string, string)>();

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;

            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            var props = ni.GetIPProperties();
            foreach (var addr in props.UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    var ip = addr.Address.ToString();
                    var mask = addr.IPv4Mask?.ToString() ?? "255.255.255.0";
                    result.Add((ni.Name, ip, mask));
                }
            }
        }

        return result;
    }

    public static string GetDhcpInfo(string iface)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetDhcpInfoMacOS(iface);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetDhcpInfoLinux(iface);
            }
        }
        catch { }

        return "Unknown";
    }

    private static string GetDhcpInfoMacOS(string iface)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ipconfig",
            Arguments = $"getpacket {iface}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return "Unknown";

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (string.IsNullOrWhiteSpace(output) || output.Contains("no DHCP"))
            return "Static IP";

        string server = "", lease = "";

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("server_identifier"))
            {
                var parts = trimmed.Split(':');
                if (parts.Length >= 2)
                    server = parts[1].Trim();
            }
            else if (trimmed.StartsWith("lease_time"))
            {
                var parts = trimmed.Split(':');
                if (parts.Length >= 2)
                {
                    var hex = parts[1].Trim();
                    if (hex.StartsWith("0x") && int.TryParse(hex.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out var seconds))
                    {
                        var hours = seconds / 3600;
                        var mins = (seconds % 3600) / 60;
                        lease = hours > 0 ? $"{hours}h{mins}m" : $"{mins}m";
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(server))
            return $"Server: {server} | Lease: {lease}";

        return "Static IP";
    }

    private static string GetDhcpInfoLinux(string iface)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "nmcli",
            Arguments = $"-t -f IP4.ADDRESS,IP4.GATEWAY,DHCP4.OPTION device show {iface}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null) return "Unknown";

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            string server = "", lease = "";

            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("dhcp_server_identifier"))
                {
                    var parts = line.Split('=');
                    if (parts.Length >= 2)
                        server = parts[1].Trim();
                }
                else if (line.Contains("expiry") || line.Contains("lease_time"))
                {
                    var parts = line.Split('=');
                    if (parts.Length >= 2)
                        lease = parts[1].Trim();
                }
            }

            if (!string.IsNullOrEmpty(server))
                return $"Server: {server} | Lease: {lease}";

            return "Static IP or Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}
