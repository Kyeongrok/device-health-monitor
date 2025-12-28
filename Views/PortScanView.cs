using System.Collections.Concurrent;
using Terminal.Gui;
using DHM.Service;

namespace DHM.Views;

public class PortScanView
{
    private string _targetIp = "192.168.1.100";
    private int _startPort = 1;
    private int _endPort = 100;
    private int _timeout = 100;
    private int _throttleLimit = 50;

    private ConcurrentDictionary<int, bool> _portStatus = new();
    private List<string> _portItems = new();
    private ListView _portListView = null!;
    private Label _statusLabel = null!;
    private ProgressBar _progressBar = null!;

    public static void Show()
    {
        var view = new PortScanView();
        view.ShowDialog();
    }

    public static void Show(string targetIp, int startPort, int endPort)
    {
        var view = new PortScanView
        {
            _targetIp = targetIp,
            _startPort = startPort,
            _endPort = endPort
        };
        view.ShowDialog();
    }

    private void ShowDialog()
    {
        var dialog = new Dialog("Port Scan", 70, 22);

        // 설정 영역
        var ipLabel = new Label("Target IP:") { X = 1, Y = 0 };
        var ipField = new TextField(_targetIp) { X = 12, Y = 0, Width = 18 };

        var portsLabel = new Label("Ports:") { X = 32, Y = 0 };
        var startField = new TextField(_startPort.ToString()) { X = 39, Y = 0, Width = 6 };
        var dashLabel = new Label("-") { X = 46, Y = 0 };
        var endField = new TextField(_endPort.ToString()) { X = 48, Y = 0, Width = 6 };

        var scanBtn = new Button("Scan") { X = 56, Y = 0 };

        // 상태
        _statusLabel = new Label("Ready. Press Scan to start.")
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill()
        };

        _progressBar = new ProgressBar()
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill() - 2,
            Height = 1
        };

        // 결과 리스트
        _portListView = new ListView(_portItems)
        {
            X = 0,
            Y = 4,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 5
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

            _targetIp = ipField.Text.ToString() ?? _targetIp;
            int.TryParse(startField.Text.ToString(), out _startPort);
            int.TryParse(endField.Text.ToString(), out _endPort);

            if (_startPort < 1) _startPort = 1;
            if (_endPort > 65535) _endPort = 65535;
            if (_startPort > _endPort) (_startPort, _endPort) = (_endPort, _startPort);

            _portItems.Clear();
            _portItems.Add("  Scanning...");
            _portListView.SetSource(_portItems);

            _ = Task.Run(async () =>
            {
                await ScanPorts();
                isScanning = false;
            });
        };

        dialog.Add(
            ipLabel, ipField, portsLabel, startField, dashLabel, endField, scanBtn,
            _statusLabel, _progressBar, _portListView, closeBtn
        );

        Application.Run(dialog);
    }

    private async Task ScanPorts()
    {
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

        var openPorts = new ConcurrentBag<int>();

        scanner.OnProgress += (scanned, total) =>
        {
            var percent = (int)((double)scanned / total * 100);
            Application.MainLoop?.Invoke(() =>
            {
                _progressBar.Fraction = (float)scanned / total;
                _statusLabel.Text = $"Scanning {_targetIp}:{_startPort}-{_endPort}... {percent}% (Open: {openPorts.Count})";
            });
        };

        scanner.OnComplete += (portStatus) =>
        {
            _portStatus = portStatus;
            Application.MainLoop?.Invoke(() =>
            {
                UpdatePortList();
                _statusLabel.Text = $"Scan complete. Found {_portStatus.Count} open ports.";
                _progressBar.Fraction = 1;
            });
        };

        // 실시간으로 열린 포트 표시를 위해 PortScanService 수정 필요
        // 일단 기존 방식으로 진행
        await scanner.ScanAsync(_targetIp, _startPort, _endPort);
    }

    private void UpdatePortList()
    {
        _portItems.Clear();

        var openPorts = _portStatus.Where(p => p.Value).OrderBy(p => p.Key).ToList();

        if (openPorts.Count == 0)
        {
            _portItems.Add("  No open ports found");
        }
        else
        {
            _portItems.Add($"  Found {openPorts.Count} open ports on {_targetIp}");
            _portItems.Add("  " + new string('-', 50));

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
