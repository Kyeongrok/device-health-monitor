using Terminal.Gui;
using DHM.Views;
using DHM.Service;

class Program
{
    static void Main(string[] args)
    {
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
            Height = Dim.Fill()
        };

        var menuList = new ListView(new[] { "Port Scan", "IP Scan", "Wi-Fi Scan", "About", "Quit" })
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
                    PortScanView.Show();
                    break;
                case 1: // IP Scan
                    IpScanView.Show();
                    break;
                case 2: // Wi-Fi Scan
                    WifiScanView.Show();
                    break;
                case 3: // About
                    MessageBox.Query("About", "DHM - Device Health Monitor\n\nNetwork scanning utility with TUI\n\nVersion 1.0", "OK");
                    break;
                case 4: // Quit
                    Application.RequestStop();
                    break;
            }
        };

        menuFrame.Add(menuList);

        // 오른쪽 콘텐츠 프레임 - 네트워크 정보
        var contentFrame = new FrameView("Network Info")
        {
            X = 20,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // 로컬 네트워크 정보
        var networkInfo = NetworkService.GetLocalNetworkInfo();
        var localIpText = string.Join(" | ", networkInfo.Select(n => $"{n.Name}: {n.IP}"));
        if (string.IsNullOrEmpty(localIpText))
            localIpText = "No network interface found";

        var localIpLabel = new Label($"Local: {localIpText}")
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill()
        };

        // 현재 연결된 Wi-Fi 정보
        var currentWifi = WifiService.GetCurrentConnection();
        var wifiLabel = new Label($"Wi-Fi: {currentWifi}")
        {
            X = 1,
            Y = 3,
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
            X = 1,
            Y = 5,
            Width = Dim.Fill(),
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.BrightCyan, Color.Black)
            }
        };

        // 도움말
        var helpLabel = new Label("Select a menu item and press Enter to start.")
        {
            X = 1,
            Y = 8,
            Width = Dim.Fill()
        };

        var shortcutLabel = new Label("Shortcuts: Q - Quit")
        {
            X = 1,
            Y = 10,
            Width = Dim.Fill(),
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.Gray, Color.Black)
            }
        };

        contentFrame.Add(localIpLabel, wifiLabel, dhcpLabel, helpLabel, shortcutLabel);

        mainWin.Add(menuFrame, contentFrame);
        top.Add(mainWin);

        // 단축키
        top.KeyPress += (e) =>
        {
            if (e.KeyEvent.Key == Key.Q || e.KeyEvent.Key == Key.q)
            {
                Application.RequestStop();
                e.Handled = true;
            }
        };

        Application.Run();
        Application.Shutdown();
    }
}
