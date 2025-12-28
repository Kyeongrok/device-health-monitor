using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DHM.Service;

public static class WifiService
{
    public static string GetCurrentConnection()
    {
        try
        {
            List<string> connections;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                connections = GetCurrentConnectionsMacOS();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                connections = GetCurrentConnectionsLinux();
            }
            else
            {
                return "Not supported";
            }

            if (connections.Count == 0)
                return "Not connected";

            return string.Join(" | ", connections);
        }
        catch { }

        return "Not connected";
    }

    public static List<string> ScanWifiNetworks()
    {
        var networks = new List<string>();

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                networks = ScanWifiMacOS();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                networks = ScanWifiLinux();
            }
            else
            {
                networks.Add("  Wi-Fi scan not supported on this platform");
            }
        }
        catch (Exception ex)
        {
            networks.Add($"  Error: {ex.Message}");
        }

        if (networks.Count == 0)
        {
            networks.Add("  No Wi-Fi networks found");
        }

        return networks;
    }

    private static List<string> GetCurrentConnectionsMacOS()
    {
        var connections = new List<string>();

        var psi = new ProcessStartInfo
        {
            FileName = "system_profiler",
            Arguments = "SPAirPortDataType",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return connections;

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var lines = output.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("Current Network Information:") && i + 1 < lines.Length)
            {
                var ssidLine = lines[i + 1].Trim();
                if (ssidLine.EndsWith(":"))
                {
                    var ssid = ssidLine.TrimEnd(':');

                    string phyMode = "", channel = "";
                    for (int j = i + 2; j < lines.Length && j < i + 10; j++)
                    {
                        var line = lines[j].Trim();
                        if (line.StartsWith("PHY Mode:"))
                            phyMode = line.Replace("PHY Mode:", "").Trim();
                        else if (line.StartsWith("Channel:"))
                            channel = line.Replace("Channel:", "").Trim();
                        else if (line.EndsWith(":") && !line.Contains(":"))
                            break;
                    }

                    if (!string.IsNullOrEmpty(ssid))
                        connections.Add($"{ssid} ({phyMode}, {channel})");
                }
            }
        }

        return connections;
    }

    private static List<string> GetCurrentConnectionsLinux()
    {
        var connections = new List<string>();

        var psi = new ProcessStartInfo
        {
            FileName = "nmcli",
            Arguments = "-t -f active,ssid,signal,chan d wifi",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null) return GetCurrentConnectionsLinuxIwgetid();

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            foreach (var line in output.Split('\n'))
            {
                if (line.StartsWith("yes:"))
                {
                    var parts = line.Split(':');
                    if (parts.Length >= 4)
                    {
                        var ssid = parts[1];
                        var signal = parts[2];
                        var channel = parts[3];
                        connections.Add($"{ssid} ({signal}%, CH {channel})");
                    }
                }
            }
        }
        catch
        {
            return GetCurrentConnectionsLinuxIwgetid();
        }

        return connections;
    }

    private static List<string> GetCurrentConnectionsLinuxIwgetid()
    {
        var connections = new List<string>();
        var interfaces = new[] { "wlan0", "wlan1", "wlp0s20f3", "wlp2s0" };

        foreach (var iface in interfaces)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "iwgetid",
                Arguments = $"{iface} -r",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(psi);
                if (process == null) continue;

                var ssid = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(ssid))
                    connections.Add($"{ssid} ({iface})");
            }
            catch { }
        }

        return connections;
    }

    private static List<string> ScanWifiMacOS()
    {
        var networks = new List<string>();

        var psi = new ProcessStartInfo
        {
            FileName = "/System/Library/PrivateFrameworks/Apple80211.framework/Versions/Current/Resources/airport",
            Arguments = "-s",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return networks;

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        networks.Add("  SSID                             RSSI  CH  SECURITY");
        networks.Add("  " + new string('-', 65));

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var trimmed = line.TrimStart();
            var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 7)
            {
                var ssid = parts[0].PadRight(32);
                var rssi = int.TryParse(parts[1], out var r) ? r : -100;
                var channel = parts[2].PadLeft(3);
                var security = string.Join(" ", parts.Skip(6));

                var signalBar = GetSignalBar(rssi);
                networks.Add($"  {ssid} {signalBar} {rssi,4}  {channel}  {security}");
            }
        }

        return networks;
    }

    private static List<string> ScanWifiLinux()
    {
        var networks = new List<string>();

        var psi = new ProcessStartInfo
        {
            FileName = "nmcli",
            Arguments = "-t -f SSID,SIGNAL,CHAN,SECURITY d wifi list",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null) return ScanWifiLinuxIwlist();

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                return ScanWifiLinuxIwlist();
            }

            networks.Add("  SSID                             SIGNAL  CH  SECURITY");
            networks.Add("  " + new string('-', 65));

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split(':');
                if (parts.Length >= 4)
                {
                    var ssid = (string.IsNullOrEmpty(parts[0]) ? "(Hidden)" : parts[0]).PadRight(32);
                    var signal = int.TryParse(parts[1], out var s) ? s : 0;
                    var channel = parts[2].PadLeft(3);
                    var security = parts[3];

                    var signalBar = GetSignalBar(-100 + signal);
                    networks.Add($"  {ssid} {signalBar} {signal,3}%  {channel}  {security}");
                }
            }
        }
        catch
        {
            return ScanWifiLinuxIwlist();
        }

        return networks;
    }

    private static List<string> ScanWifiLinuxIwlist()
    {
        var networks = new List<string>();

        var psi = new ProcessStartInfo
        {
            FileName = "iwlist",
            Arguments = "wlan0 scan",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null)
            {
                networks.Add("  Failed to start iwlist");
                return networks;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            networks.Add("  SSID                             SIGNAL");
            networks.Add("  " + new string('-', 45));

            string? currentSsid = null;
            string? currentSignal = null;

            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("ESSID:"))
                {
                    currentSsid = trimmed.Replace("ESSID:", "").Trim('"');
                }
                else if (trimmed.Contains("Signal level="))
                {
                    var idx = trimmed.IndexOf("Signal level=");
                    if (idx >= 0)
                    {
                        currentSignal = trimmed.Substring(idx + 13).Split(' ')[0];
                    }
                }

                if (currentSsid != null && currentSignal != null)
                {
                    networks.Add($"  {currentSsid.PadRight(32)} {currentSignal}");
                    currentSsid = null;
                    currentSignal = null;
                }
            }

            if (networks.Count == 2)
            {
                networks.Add("  No networks found. Try: sudo iwlist wlan0 scan");
            }
        }
        catch (Exception ex)
        {
            networks.Add($"  Error: {ex.Message}");
            networks.Add("  Install: sudo apt install wireless-tools");
        }

        return networks;
    }

    public static string GetSignalBar(int rssi)
    {
        int bars;
        if (rssi >= -50) bars = 4;
        else if (rssi >= -60) bars = 3;
        else if (rssi >= -70) bars = 2;
        else if (rssi >= -80) bars = 1;
        else bars = 0;

        return new string('█', bars) + new string('░', 4 - bars);
    }
}
