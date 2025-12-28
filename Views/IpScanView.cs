using Terminal.Gui;
using DHM.Service;

namespace DHM.Views;

public class IpScanView
{
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
        var statusLabel = new Label("Ready. Press Scan to start.")
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill()
        };

        // 프로그레스 바
        var progressBar = new ProgressBar()
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill() - 2,
            Height = 1
        };

        // 결과 리스트
        var resultList = new ListView()
        {
            X = 0,
            Y = 4,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 6
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

            statusLabel.Text = "Scanning...";
            progressBar.Fraction = 0;

            var results = new List<string>
            {
                "  IP Address        Hostname                    RTT    MAC Address        Ports",
                "  " + new string('-', 75)
            };
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
                        statusLabel.Text = $"Scanning {network}.{start}-{end}... {scanned}/{total} (Found: {foundCount})";
                    });
                };

                // 실시간으로 호스트 발견 시 표시
                scanner.OnHostFound += (host) =>
                {
                    Interlocked.Increment(ref foundCount);
                    Application.MainLoop?.Invoke(() =>
                    {
                        var ip = host.IpAddress.PadRight(16);
                        var hostname = (host.Hostname ?? "").PadRight(26);
                        if (hostname.Length > 26) hostname = hostname.Substring(0, 23) + "...";
                        var rtt = $"{host.ResponseTime}ms".PadRight(6);
                        var mac = (host.MacAddress ?? "").PadRight(18);
                        var ports = host.GetPortsDisplay();

                        results.Add($"  {ip}  {hostname}  {rtt} {mac} {ports}");
                        resultList.SetSource(results);
                    });
                };

                scanner.OnComplete += (hosts) =>
                {
                    Application.MainLoop?.Invoke(() =>
                    {
                        statusLabel.Text = $"Scan complete. Found {hosts.Count} hosts.";
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

        Application.Run(dialog);
    }
}
