using Terminal.Gui;
using DHM.Service;

namespace DHM.Views;

public class IpScanView
{
    private static List<HostInfo> _foundHosts = new();

    public static void Show()
    {
        // 현재 IP에서 네트워크 대역 추출
        var networkInfo = NetworkService.GetLocalNetworkInfo();
        var currentIp = networkInfo.FirstOrDefault().IP ?? "192.168.1.1";
        var networkPrefix = IpScanService.GetNetworkPrefix(currentIp);

        var dialog = new Dialog("IP Scan - Network Discovery", 80, 24);

        // 네트워크 대역 입력
        var networkLabel = new Label("Network:")
        {
            X = 1,
            Y = 0
        };

        var networkField = new TextField($"{networkPrefix}.")
        {
            X = 10,
            Y = 0,
            Width = 15
        };

        var rangeLabel = new Label("Range:")
        {
            X = 27,
            Y = 0
        };

        var startField = new TextField("1")
        {
            X = 34,
            Y = 0,
            Width = 5
        };

        var dashLabel = new Label("-")
        {
            X = 40,
            Y = 0
        };

        var endField = new TextField("254")
        {
            X = 42,
            Y = 0,
            Width = 5
        };

        var scanBtn = new Button("Scan")
        {
            X = 50,
            Y = 0
        };

        // 상태 라벨
        var statusLabel = new Label("Ready. Press Scan to start. (Enter: View details)")
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill()
        };

        // 색상 스킴 정의
        var inProgressColor = new ColorScheme
        {
            Normal = Application.Driver.MakeAttribute(Color.BrightGreen, Color.Black)
        };
        var completeColor = new ColorScheme
        {
            Normal = Application.Driver.MakeAttribute(Color.BrightCyan, Color.Black)
        };
        var defaultColor = statusLabel.ColorScheme;

        // 프로그레스 바
        var progressBar = new ProgressBar()
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill() - 2,
            Height = 1
        };

        // 결과 리스트
        var results = new List<string>();
        var resultList = new ListView(results)
        {
            X = 0,
            Y = 4,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 6
        };

        // 엔터 누르면 상세 정보 표시
        resultList.OpenSelectedItem += (args) =>
        {
            var index = args.Item - 2; // 헤더 2줄 제외
            if (index >= 0 && index < _foundHosts.Count)
            {
                ShowHostDetail(_foundHosts[index]);
            }
        };

        // 버튼
        var closeBtn = new Button("Close")
        {
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1)
        };

        closeBtn.Clicked += () => Application.RequestStop();

        var isScanning = false;

        scanBtn.Clicked += () =>
        {
            if (isScanning) return;
            isScanning = true;

            var network = networkField.Text.ToString()?.TrimEnd('.') ?? networkPrefix;
            int.TryParse(startField.Text.ToString(), out var start);
            int.TryParse(endField.Text.ToString(), out var end);

            if (start < 1) start = 1;
            if (end > 254) end = 254;
            if (start > end) (start, end) = (end, start);

            statusLabel.Text = "⏳ In Progress...";
            statusLabel.ColorScheme = inProgressColor;
            progressBar.Fraction = 0;

            results.Clear();
            _foundHosts.Clear();
            results.Add("  IP Address        Hostname                 RTT    MAC");
            results.Add("  " + new string('-', 60));
            resultList.SetSource(results);

            var foundCount = 0;

            _ = Task.Run(async () =>
            {
                var scanner = new IpScanService
                {
                    Timeout = 1000,
                    ThrottleLimit = 50
                };

                scanner.OnProgress += (scanned, total) =>
                {
                    Application.MainLoop?.Invoke(() =>
                    {
                        progressBar.Fraction = (float)scanned / total;
                        statusLabel.Text = $"⏳ In Progress - {network}.{start}-{end}... {scanned}/{total} (Found: {foundCount})";
                        statusLabel.ColorScheme = inProgressColor;
                    });
                };

                // 실시간으로 호스트 발견 시 표시
                scanner.OnHostFound += (host) =>
                {
                    Interlocked.Increment(ref foundCount);
                    Application.MainLoop?.Invoke(() =>
                    {
                        _foundHosts.Add(host);

                        var ip = host.IpAddress.PadRight(16);
                        var hostname = (host.Hostname ?? "").PadRight(22);
                        if (hostname.Length > 22) hostname = hostname.Substring(0, 19) + "...";
                        var rtt = $"{host.ResponseTime}ms".PadRight(6);
                        var mac = host.MacAddress ?? "";

                        var portCount = host.OpenPorts.Count > 0 ? $" [{host.OpenPorts.Count}ports]" : "";
                        results.Add($"  {ip}  {hostname} {rtt} {mac}{portCount}");
                        resultList.SetSource(results);
                    });
                };

                scanner.OnComplete += (hosts) =>
                {
                    Application.MainLoop?.Invoke(() =>
                    {
                        statusLabel.Text = $"✓ Complete - Found {hosts.Count} hosts. (Enter: View details)";
                        statusLabel.ColorScheme = completeColor;
                        progressBar.Fraction = 1;
                        isScanning = false;
                    });
                };

                // baseIp 생성 (실제 IP처럼 만들기)
                var baseIp = $"{network}.{start}";
                await scanner.ScanSubnetAsync(baseIp, start, end);
            });
        };

        dialog.Add(
            networkLabel, networkField,
            rangeLabel, startField, dashLabel, endField,
            scanBtn, statusLabel, progressBar,
            resultList, closeBtn
        );

        scanBtn.SetFocus();
        Application.Run(dialog);
    }

    private static void ShowHostDetail(HostInfo host)
    {
        var detailDialog = new Dialog("Host Details", 60, 18);

        var ipLabel = new Label($"IP Address: {host.IpAddress}")
        {
            X = 1,
            Y = 1
        };

        var hostnameLabel = new Label($"Hostname:   {host.Hostname ?? "(unknown)"}")
        {
            X = 1,
            Y = 2
        };

        var macLabel = new Label($"MAC:        {host.MacAddress ?? "(unknown)"}")
        {
            X = 1,
            Y = 3
        };

        var rttLabel = new Label($"RTT:        {host.ResponseTime}ms")
        {
            X = 1,
            Y = 4
        };

        var portsTitle = new Label("Open Ports:")
        {
            X = 1,
            Y = 6
        };

        var portsList = new List<string>();
        if (host.OpenPorts.Count == 0)
        {
            portsList.Add("  No open ports detected");
        }
        else
        {
            foreach (var port in host.OpenPorts)
            {
                var portName = PortScanService.GetPortName(port);
                var nameStr = string.IsNullOrEmpty(portName) ? "" : $" ({portName})";
                portsList.Add($"  {port}{nameStr}");
            }
        }

        var portsListView = new ListView(portsList)
        {
            X = 1,
            Y = 7,
            Width = Dim.Fill() - 2,
            Height = 5
        };

        var portScanBtn = new Button("Port Scan")
        {
            X = Pos.Center() - 12,
            Y = Pos.AnchorEnd(1)
        };

        portScanBtn.Clicked += () =>
        {
            Application.RequestStop();
            PortScanView.Show(host.IpAddress, 1, 1000);
        };

        var closeBtn = new Button("Close")
        {
            X = Pos.Center() + 3,
            Y = Pos.AnchorEnd(1)
        };

        closeBtn.Clicked += () => Application.RequestStop();

        detailDialog.Add(ipLabel, hostnameLabel, macLabel, rttLabel, portsTitle, portsListView, portScanBtn, closeBtn);
        Application.Run(detailDialog);
    }
}
