using Terminal.Gui;
using DHM.Views;
using DHM.Service;

class Program
{
    // 네트워크 정보 캐시
    private static string _localIpText = "Loading...";
    private static string _currentWifi = "Loading...";
    private static string _dhcpInfo = "Loading...";

    // UI 요소
    private static Label? _localIpLabel;
    private static Label? _wifiLabel;
    private static Label? _dhcpLabel;
    private static Label? _statusLabel;
    private static ProgressBar? _progressBar;
    private static FrameView? _loadingFrame;
    private static FrameView? _contentFrame;

    static void Main(string[] args)
    {
        Application.Init();
        SetupColors();

        var top = Application.Top;

        // 상단 메뉴바
        var menuBar = new MenuBar(new MenuBarItem[]
        {
            new MenuBarItem("_File", new MenuItem[]
            {
                new MenuItem("_Quit", "Q", () => Application.RequestStop())
            }),
            new MenuBarItem("_Help", new MenuItem[]
            {
                new MenuItem("_About", "", () =>
                    MessageBox.Query("About", "DHM - Device Health Monitor\n\nNetwork scanning utility with TUI\n\nVersion 1.0\n\nShortcuts:\n  F10 - Menu Bar\n  Q - Quit", "OK"))
            })
        });

        // 메인 윈도우
        var mainWin = new Window("DHM - Device Health Monitor")
        {
            X = 0,
            Y = 1, // 메뉴바 아래로
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // 왼쪽 메뉴 프레임
        var menuFrame = new FrameView("Menu")
        {
            X = 0,
            Y = 0,
            Width = 20,
            Height = Dim.Fill()
        };

        var menuList = new ListView(new[] { "Port Scan", "IP Scan", "Wi-Fi Scan", "Quit" })
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
                case 0: PortScanView.Show(); break;
                case 1: IpScanView.Show(); break;
                case 2: WifiScanView.Show(); break;
                case 3: Application.RequestStop(); break;
            }
        };

        // 최상단에서 Up 누르면 메뉴바로 이동
        menuList.KeyPress += (e) =>
        {
            if (e.KeyEvent.Key == Key.CursorUp && menuList.SelectedItem == 0)
            {
                menuBar.OpenMenu();
                e.Handled = true;
            }
        };

        menuFrame.Add(menuList);

        // 오른쪽 로딩 프레임
        _loadingFrame = new FrameView("Loading...")
        {
            X = 20,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        _statusLabel = new Label("Initializing...")
        {
            X = Pos.Center(),
            Y = Pos.Center() - 1
        };

        _progressBar = new ProgressBar()
        {
            X = 4,
            Y = Pos.Center() + 1,
            Width = Dim.Fill() - 8,
            Height = 1
        };

        _loadingFrame.Add(_statusLabel, _progressBar);

        // 오른쪽 콘텐츠 프레임 (처음엔 숨김)
        _contentFrame = new FrameView("Network Info")
        {
            X = 20,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Visible = false
        };

        _localIpLabel = new Label("Local: Loading...")
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill()
        };

        _wifiLabel = new Label("Wi-Fi: Loading...")
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(),
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.BrightGreen, Color.Black)
            }
        };

        _dhcpLabel = new Label("DHCP: Loading...")
        {
            X = 1,
            Y = 5,
            Width = Dim.Fill(),
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.BrightCyan, Color.Black)
            }
        };

        var helpLabel = new Label("Select a menu item and press Enter to start.")
        {
            X = 1,
            Y = 8,
            Width = Dim.Fill()
        };

        var shortcutLabel = new Label("Shortcuts: F10 - Menu Bar, Q - Quit")
        {
            X = 1,
            Y = 10,
            Width = Dim.Fill(),
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.Gray, Color.Black)
            }
        };

        _contentFrame.Add(_localIpLabel, _wifiLabel, _dhcpLabel, helpLabel, shortcutLabel);

        mainWin.Add(menuFrame, _loadingFrame, _contentFrame);
        top.Add(menuBar, mainWin);

        // 단축키
        top.KeyPress += (e) =>
        {
            if (e.KeyEvent.Key == Key.Q || e.KeyEvent.Key == Key.q)
            {
                Application.RequestStop();
                e.Handled = true;
            }
        };

        // 백그라운드에서 네트워크 정보 로드
        _ = Task.Run(LoadNetworkInfoAsync);

        Application.Run();
        Application.Shutdown();
    }

    static async Task LoadNetworkInfoAsync()
    {
        // 1. 네트워크 정보 로드
        Application.MainLoop?.Invoke(() =>
        {
            if (_statusLabel != null) _statusLabel.Text = "Loading network info...";
            if (_progressBar != null) _progressBar.Fraction = 0.2f;
        });

        var networkInfo = NetworkService.GetLocalNetworkInfo();
        _localIpText = string.Join(" | ", networkInfo.Select(n => $"{n.Name}: {n.IP}"));
        if (string.IsNullOrEmpty(_localIpText))
            _localIpText = "No network interface found";

        await Task.Delay(100);

        // 2. Wi-Fi 정보 로드
        Application.MainLoop?.Invoke(() =>
        {
            if (_statusLabel != null) _statusLabel.Text = "Loading Wi-Fi info...";
            if (_progressBar != null) _progressBar.Fraction = 0.5f;
        });

        _currentWifi = WifiService.GetCurrentConnection();

        await Task.Delay(100);

        // 3. DHCP 정보 로드
        Application.MainLoop?.Invoke(() =>
        {
            if (_statusLabel != null) _statusLabel.Text = "Loading DHCP info...";
            if (_progressBar != null) _progressBar.Fraction = 0.8f;
        });

        _dhcpInfo = NetworkService.GetDhcpInfo("en0");

        await Task.Delay(100);

        // 완료 - UI 전환
        Application.MainLoop?.Invoke(() =>
        {
            if (_statusLabel != null) _statusLabel.Text = "Ready!";
            if (_progressBar != null) _progressBar.Fraction = 1.0f;
        });

        await Task.Delay(300);

        // 로딩 프레임 숨기고 콘텐츠 프레임 표시
        Application.MainLoop?.Invoke(() =>
        {
            if (_loadingFrame != null) _loadingFrame.Visible = false;
            if (_contentFrame != null) _contentFrame.Visible = true;

            if (_localIpLabel != null) _localIpLabel.Text = $"Local: {_localIpText}";
            if (_wifiLabel != null) _wifiLabel.Text = $"Wi-Fi: {_currentWifi}";
            if (_dhcpLabel != null) _dhcpLabel.Text = $"DHCP: {_dhcpInfo}";
        });
    }

    static void SetupColors()
    {
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
    }
}
