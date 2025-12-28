using System.Collections.Concurrent;
using Terminal.Gui;
using DHM.Views;
using DHM.Service;

class Program
{
    private static string _targetIp = "192.168.1.100";
    private static int _startPort = 1;
    private static int _endPort = 100;
    private static int _refreshInterval = 5;
    private static int _timeout = 100;
    private static int _throttleLimit = 50;

    private static ConcurrentDictionary<int, bool> _portStatus = new();
    private static int _scanProgress = 0;
    private static bool _running = true;

    private static ListView _portListView = null!;
    private static Label _statusLabel = null!;
    private static ProgressBar _progressBar = null!;
    private static Label _infoLabel = null!;
    private static List<string> _portItems = new();

    static void Main(string[] args)
    {
        ParseArgs(args);

        Application.Init();

        // 색상 테마 설정
        Colors.Base = new ColorScheme
        {
            Normal = Application.Driver.MakeAttribute(Color.White, Color.Black),
            Focus = Application.Driver.MakeAttribute(Color.Black, Color.Cyan),
            HotNormal = Application.Driver.MakeAttribute(Color.BrightYellow, Color.Black),
            HotFocus = Application.Driver.MakeAttribute(Color.BrightYellow, Color.Cyan),
            Disabled = Application.Driver.MakeAttribute(Color.Gray, Color.Black)
        };

        Colors.Menu = new ColorScheme
        {
            Normal = Application.Driver.MakeAttribute(Color.White, Color.DarkGray),
            Focus = Application.Driver.MakeAttribute(Color.Black, Color.White),
            HotNormal = Application.Driver.MakeAttribute(Color.BrightYellow, Color.DarkGray),
            HotFocus = Application.Driver.MakeAttribute(Color.BrightYellow, Color.White),
            Disabled = Application.Driver.MakeAttribute(Color.Gray, Color.DarkGray)
        };

        Colors.Dialog = new ColorScheme
        {
            Normal = Application.Driver.MakeAttribute(Color.White, Color.DarkGray),
            Focus = Application.Driver.MakeAttribute(Color.Black, Color.White),
            HotNormal = Application.Driver.MakeAttribute(Color.BrightYellow, Color.DarkGray),
            HotFocus = Application.Driver.MakeAttribute(Color.BrightYellow, Color.White),
            Disabled = Application.Driver.MakeAttribute(Color.Gray, Color.DarkGray)
        };

        var top = Application.Top;

        // 메인 윈도우
        var mainWin = new Window("DHM - Device Health Monitor")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // 왼쪽 메뉴 프레임
        var menuFrame = new FrameView("Menu")
        {
            X = 0,
            Y = 0,
            Width = 20,
            Height = Dim.Fill() - 3
        };

        var menuList = new ListView(new[] { "Port Scan", "Wi-Fi Scan", "Settings", "About", "Quit" })
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        menuList.OpenSelectedItem += (args) =>
        {
            switch (args.Item)
            {
                case 0: // Port Scan
                    _ = Task.Run(ScanPorts);
                    break;
                case 1: // Wi-Fi Scan
                    WifiScanView.Show();
                    break;
                case 2: // Settings
                    ShowSettingsDialog();
                    break;
                case 3: // About
                    MessageBox.Query("About", "DHM - Device Health Monitor\n\nPort scanning utility with TUI", "OK");
                    break;
                case 4: // Quit
                    _running = false;
                    Application.RequestStop();
                    break;
            }
        };

        menuFrame.Add(menuList);

        // 오른쪽 콘텐츠 프레임
        var contentFrame = new FrameView("Port Status")
        {
            X = 20,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 3
        };

        // 로컬 네트워크 정보
        var networkInfo = NetworkService.GetLocalNetworkInfo();
        var localIpText = string.Join(" | ", networkInfo.Select(n => $"{n.Name}: {n.IP}"));
        if (string.IsNullOrEmpty(localIpText))
            localIpText = "No network interface found";

        var localIpLabel = new Label($"Local: {localIpText}")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill()
        };

        // 현재 연결된 Wi-Fi 정보
        var currentWifi = WifiService.GetCurrentConnection();
        var wifiLabel = new Label($"Wi-Fi: {currentWifi}")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.BrightGreen, Color.Black)
            }
        };

        // DHCP 정보
        var dhcpInfo = NetworkService.GetDhcpInfo("en0");
        var dhcpLabel = new Label($"DHCP: {dhcpInfo}")
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.BrightCyan, Color.Black)
            }
        };

        // 정보 라벨
        _infoLabel = new Label($"Target: {_targetIp} | Ports: {_startPort}-{_endPort} | Refresh: {_refreshInterval}s")
        {
            X = 0,
            Y = 3,
            Width = Dim.Fill()
        };

        // 포트 리스트
        _portListView = new ListView(_portItems)
        {
            X = 0,
            Y = 5,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 5
        };

        contentFrame.Add(localIpLabel, wifiLabel, dhcpLabel, _infoLabel, _portListView);

        // 하단 상태바
        var statusFrame = new FrameView("Status")
        {
            X = 0,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill(),
            Height = 3
        };

        _progressBar = new ProgressBar()
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(50),
            Height = 1
        };

        _statusLabel = new Label("Ready")
        {
            X = Pos.Right(_progressBar) + 2,
            Y = 0
        };

        statusFrame.Add(_progressBar, _statusLabel);

        mainWin.Add(menuFrame, contentFrame, statusFrame);
        top.Add(mainWin);

        // 단축키
        top.KeyPress += (e) =>
        {
            if (e.KeyEvent.Key == Key.Q || e.KeyEvent.Key == Key.q)
            {
                _running = false;
                Application.RequestStop();
                e.Handled = true;
            }
            else if (e.KeyEvent.Key == Key.R || e.KeyEvent.Key == Key.r)
            {
                _ = Task.Run(ScanPorts);
                e.Handled = true;
            }
            else if (e.KeyEvent.Key == Key.S || e.KeyEvent.Key == Key.s)
            {
                ShowSettingsDialog();
                e.Handled = true;
            }
        };

        // 스캔 루프 시작
        _ = Task.Run(ScanLoop);

        Application.Run();
        Application.Shutdown();
    }

    static void ParseArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-ip" when i + 1 < args.Length:
                    _targetIp = args[++i];
                    break;
                case "-ports" when i + 1 < args.Length:
                    var parts = args[++i].Split('-');
                    if (parts.Length == 2)
                    {
                        _startPort = int.Parse(parts[0]);
                        _endPort = int.Parse(parts[1]);
                    }
                    break;
                case "-interval" when i + 1 < args.Length:
                    _refreshInterval = int.Parse(args[++i]);
                    break;
            }
        }
    }

    static void ShowSettingsDialog()
    {
        var dialog = new Dialog("Settings", 50, 15);

        var ipLabel = new Label("Target IP:") { X = 1, Y = 1 };
        var ipField = new TextField(_targetIp) { X = 15, Y = 1, Width = 30 };

        var portsLabel = new Label("Port Range:") { X = 1, Y = 3 };
        var startField = new TextField(_startPort.ToString()) { X = 15, Y = 3, Width = 10 };
        var dashLabel = new Label("-") { X = 26, Y = 3 };
        var endField = new TextField(_endPort.ToString()) { X = 28, Y = 3, Width = 10 };

        var intervalLabel = new Label("Interval(s):") { X = 1, Y = 5 };
        var intervalField = new TextField(_refreshInterval.ToString()) { X = 15, Y = 5, Width = 10 };

        var okBtn = new Button("OK")
        {
            X = Pos.Center() - 10,
            Y = 8
        };
        okBtn.Clicked += () =>
        {
            _targetIp = ipField.Text.ToString() ?? _targetIp;
            int.TryParse(startField.Text.ToString(), out _startPort);
            int.TryParse(endField.Text.ToString(), out _endPort);
            int.TryParse(intervalField.Text.ToString(), out _refreshInterval);

            Application.MainLoop.Invoke(() =>
            {
                _infoLabel.Text = $"Target: {_targetIp} | Ports: {_startPort}-{_endPort} | Refresh: {_refreshInterval}s";
            });

            Application.RequestStop();
        };

        var cancelBtn = new Button("Cancel")
        {
            X = Pos.Center() + 5,
            Y = 8
        };
        cancelBtn.Clicked += () => Application.RequestStop();

        dialog.Add(ipLabel, ipField, portsLabel, startField, dashLabel, endField, intervalLabel, intervalField, okBtn, cancelBtn);
        Application.Run(dialog);
    }

    static async Task ScanLoop()
    {
        while (_running)
        {
            await ScanPorts();

            for (int i = 0; i < _refreshInterval * 10 && _running; i++)
            {
                var remaining = _refreshInterval - (i / 10);
                Application.MainLoop?.Invoke(() =>
                {
                    _statusLabel.Text = $"Next scan in {remaining}s | R:refresh S:settings Q:quit";
                });
                await Task.Delay(100);
            }
        }
    }

    static async Task ScanPorts()
    {
        _scanProgress = 0;

        Application.MainLoop?.Invoke(() =>
        {
            _statusLabel.Text = "Scanning...";
            _progressBar.Fraction = 0;
        });

        var scanner = new PortScanService
        {
            Timeout = _timeout,
            ThrottleLimit = _throttleLimit
        };

        scanner.OnProgress += (scanned, total) =>
        {
            _scanProgress = (int)((double)scanned / total * 100);
            Application.MainLoop?.Invoke(() =>
            {
                _progressBar.Fraction = (float)scanned / total;
                _statusLabel.Text = $"Scanning... {_scanProgress}%";
            });
        };

        scanner.OnComplete += (portStatus) =>
        {
            _portStatus = portStatus;
            Application.MainLoop?.Invoke(() =>
            {
                UpdatePortList();
                _statusLabel.Text = $"Scan complete. Found {_portStatus.Count} open ports.";
            });
        };

        await scanner.ScanAsync(_targetIp, _startPort, _endPort);
    }

    static void UpdatePortList()
    {
        _portItems.Clear();

        var openPorts = _portStatus.Where(p => p.Value).OrderBy(p => p.Key).ToList();

        if (openPorts.Count == 0)
        {
            _portItems.Add("  No open ports found");
        }
        else
        {
            foreach (var port in openPorts)
            {
                var portName = PortScanService.GetPortName(port.Key);
                var nameStr = string.IsNullOrEmpty(portName) ? "" : $"  ({portName})";
                _portItems.Add($"  ● Port {port.Key,5} - OPEN{nameStr}");
            }
        }

        _portListView.SetSource(_portItems);
    }
}
