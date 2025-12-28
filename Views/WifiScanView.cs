using Terminal.Gui;
using DHM.Service;

namespace DHM.Views;

public class WifiScanView
{
    public static void Show()
    {
        var dialog = new Dialog("Wi-Fi Networks", 75, 24);

        // 현재 연결된 AP 정보
        var currentAp = WifiService.GetCurrentConnection();
        var connectedLabel = new Label($"Connected: {currentAp}")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.BrightGreen, Color.Black)
            }
        };

        var statusLabel = new Label("Scanning...")
        {
            X = Pos.Center(),
            Y = 1
        };

        var listView = new ListView()
        {
            X = 0,
            Y = 3,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 4
        };

        var refreshBtn = new Button("Refresh")
        {
            X = Pos.Center() - 12,
            Y = Pos.AnchorEnd(1)
        };

        var closeBtn = new Button("Close")
        {
            X = Pos.Center() + 3,
            Y = Pos.AnchorEnd(1)
        };

        closeBtn.Clicked += () => Application.RequestStop();

        refreshBtn.Clicked += () =>
        {
            statusLabel.Text = "Scanning...";
            Application.Refresh();

            _ = Task.Run(() =>
            {
                var networks = WifiService.ScanWifiNetworks();
                var currentApRefresh = WifiService.GetCurrentConnection();
                Application.MainLoop?.Invoke(() =>
                {
                    connectedLabel.Text = $"Connected: {currentApRefresh}";
                    listView.SetSource(networks);
                    statusLabel.Text = $"Found {networks.Count - 2} networks";
                });
            });
        };

        dialog.Add(connectedLabel, statusLabel, listView, refreshBtn, closeBtn);

        refreshBtn.SetFocus();

        // 초기 스캔
        _ = Task.Run(() =>
        {
            var networks = WifiService.ScanWifiNetworks();
            Application.MainLoop?.Invoke(() =>
            {
                listView.SetSource(networks);
                statusLabel.Text = $"Found {networks.Count - 2} networks";
            });
        });

        Application.Run(dialog);
    }
}
