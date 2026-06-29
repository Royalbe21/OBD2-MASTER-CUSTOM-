using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using TacomaDiag.Models;
using TacomaDiag.Services;

namespace TacomaDiag;

public partial class MainWindow : Window
{
    private readonly Elm327Client _elm = new();
    private readonly J2534Client _j2534 = new();
    private readonly VehicleProfile _profile = VehicleProfile.ToyotaTacoma2008Base2TrFe;
    private readonly HashSet<int> _supportedPids = [];
    private IObdTransport? _transport;
    private CancellationTokenSource? _pollingCancellation;
    private ReadinessSnapshot? _lastReadiness;
    private string _lastProtocol = "";
    private string _lastVin = "";

    public ObservableCollection<DiagnosticTroubleCode> DtcRows { get; } = [];
    public ObservableCollection<MonitorStatus> MonitorRows { get; } = [];
    public ObservableCollection<LivePidReading> LivePidRows { get; } = [];
    public ObservableCollection<J2534DeviceInfo> J2534Devices { get; } = [];

    private IObdTransport Transport => _transport ?? _elm;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _transport = _elm;
        ProfileTextBlock.Text = $"{_profile.Name} | {_profile.Engine} | {_profile.ExpectedProtocol}";
        AdapterModeComboBox.ItemsSource = new[] { "Serial ELM327", "J2534 PassThru", "Demo" };
        AdapterModeComboBox.SelectedIndex = 0;
        BaudComboBox.ItemsSource = new[] { 9600, 38400, 115200, 500000 };
        BaudComboBox.SelectedItem = 38400;
        RefreshPorts();
        SeedLivePidGrid();
        UpdateAdapterControlState();
        UpdateConnectionStatus();
        RefreshReport();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        StopPolling();
        _elm.Dispose();
        _j2534.Dispose();
    }

    private void RefreshPortsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshPorts();
    }

    private void AdapterModeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateAdapterControlState();
    }

    private void BrowseJ2534Button_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select J2534 PassThru DLL",
            Filter = "J2534 DLL (*.dll)|*.dll|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var manualDevice = J2534DeviceInfo.FromManualPath(dialog.FileName);
        J2534Devices.Add(manualDevice);
        J2534DllComboBox.SelectedItem = manualDevice;
        AdapterModeComboBox.SelectedItem = "J2534 PassThru";
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(async () =>
        {
            DisconnectTransports();

            var selectedMode = AdapterModeComboBox.SelectedItem?.ToString() ?? "Serial ELM327";
            if (selectedMode == "Demo")
            {
                _elm.ConnectDemo();
                _transport = _elm;
            }
            else if (selectedMode == "J2534 PassThru")
            {
                if (J2534DllComboBox.SelectedItem is not J2534DeviceInfo j2534Device)
                {
                    MessageBox.Show(this, "Select a J2534 DLL or use Browse DLL.", "TacomaDiag", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                _j2534.Connect(j2534Device.FunctionLibrary);
                _transport = _j2534;
            }
            else
            {
                var portName = PortComboBox.SelectedItem?.ToString();
                if (string.IsNullOrWhiteSpace(portName))
                {
                    MessageBox.Show(this, "Select a COM port or choose Demo/J2534 mode.", "TacomaDiag", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var baudRate = BaudComboBox.SelectedItem is int selectedBaud ? selectedBaud : 38400;
                _elm.ConnectSerial(portName, baudRate);
                _transport = _elm;
            }

            UpdateConnectionStatus();
            await InitializeTransportAsync();
            await ReadProtocolAsync();
            AppendTerminal("Connected.");
        });
    }

    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        StopPolling();
        DisconnectTransports();
        UpdateConnectionStatus();
        SetFooter("Disconnected.");
    }

    private async void FullScanButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(async () =>
        {
            DtcRows.Clear();
            await ReadProtocolAsync();
            await ReadVinAsync();
            await ReadCodesAsync("Stored", "03", clearExisting: false);
            await ReadCodesAsync("Pending", "07", clearExisting: false);
            await ReadCodesAsync("Permanent", "0A", clearExisting: false);
            await ReadReadinessAsync();
            RefreshReport();
        });
    }

    private async void StoredCodesButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(() => ReadCodesAsync("Stored", "03", clearExisting: true));
    }

    private async void PendingCodesButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(() => ReadCodesAsync("Pending", "07", clearExisting: true));
    }

    private async void PermanentCodesButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(() => ReadCodesAsync("Permanent", "0A", clearExisting: true));
    }

    private async void ClearCodesButton_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            this,
            "Clear emissions DTCs? This also resets readiness monitors and freeze-frame data.",
            "Confirm code clear",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        await RunUiTaskAsync(async () =>
        {
            EnsureConnected();
            var response = await Transport.SendCommandAsync("04", timeoutMs: 6000);
            AppendCodesLog("04", response);
            DtcRows.Clear();
            MonitorRows.Clear();
            _lastReadiness = null;
            RefreshReport();
        });
    }

    private async void ReadinessButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(ReadReadinessAsync);
    }

    private async void VinButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(ReadVinAsync);
    }

    private async void LoadSupportedPidsButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(LoadSupportedPidsAsync);
    }

    private async void ReadLiveOnceButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(ReadLiveDataOnceAsync);
    }

    private void StartPollingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pollingCancellation is not null)
        {
            return;
        }

        if (!int.TryParse(PollIntervalTextBox.Text, out var intervalMs) || intervalMs < 250)
        {
            intervalMs = 1000;
            PollIntervalTextBox.Text = intervalMs.ToString();
        }

        _pollingCancellation = new CancellationTokenSource();
        _ = PollLiveDataAsync(intervalMs, _pollingCancellation.Token);
        SetFooter("Live polling started.");
    }

    private void StopPollingButton_Click(object sender, RoutedEventArgs e)
    {
        StopPolling();
    }

    private async void Mode6Button_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(ReadMode6Async);
    }

    private async void ProbeModulesButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(ProbeModulesAsync);
    }

    private async void RawSendButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(async () =>
        {
            EnsureConnected();
            var command = RawCommandTextBox.Text.Trim();
            var response = await Transport.SendCommandAsync(command);
            AppendTerminal($"> {command}{Environment.NewLine}{response}");
        });
    }

    private void RefreshReportButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshReport();
    }

    private void SaveReportButton_Click(object sender, RoutedEventArgs e)
    {
        var reportDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TacomaDiag Reports");
        Directory.CreateDirectory(reportDirectory);

        var fileName = $"TacomaDiag-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
        var path = Path.Combine(reportDirectory, fileName);
        File.WriteAllText(path, ReportTextBox.Text);

        SetFooter($"Report saved: {path}");
        MessageBox.Show(this, path, "Report saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task InitializeTransportAsync()
    {
        var commands = new[] { "ATZ", "ATE0", "ATL0", "ATS0", "ATH0", "ATSP0", "ATAT1", "ATST96", "ATI" };
        foreach (var command in commands)
        {
            var response = await Transport.SendCommandAsync(command, timeoutMs: command == "ATZ" ? 6000 : 3000);
            AppendTerminal($"> {command}{Environment.NewLine}{response}");
        }
    }

    private async Task ReadProtocolAsync()
    {
        EnsureConnected();
        var response = await Transport.SendCommandAsync("ATDP");
        _lastProtocol = response.Trim();
        ConnectionStatusTextBlock.Text = $"{Transport.ConnectionName} | {_lastProtocol}";
        AppendTerminal($"> ATDP{Environment.NewLine}{response}");
        RefreshReport();
    }

    private async Task ReadVinAsync()
    {
        EnsureConnected();
        var response = await Transport.SendCommandAsync("0902", timeoutMs: 6000);
        _lastVin = ObdDecoder.DecodeVin(response);
        VinTextBlock.Text = string.IsNullOrWhiteSpace(_lastVin) ? "VIN: not read" : $"VIN: {_lastVin}";
        AppendTerminal($"> 0902{Environment.NewLine}{response}");
        RefreshReport();
    }

    private async Task ReadCodesAsync(string type, string command, bool clearExisting)
    {
        EnsureConnected();
        if (clearExisting)
        {
            DtcRows.Clear();
        }

        var response = await Transport.SendCommandAsync(command, timeoutMs: 6000);
        AppendCodesLog(command, response);
        var codes = ObdDecoder.DecodeDtcResponse(response, type);

        foreach (var code in codes)
        {
            DtcRows.Add(code);
        }

        if (codes.Count == 0)
        {
            DtcRows.Add(new DiagnosticTroubleCode
            {
                Type = type,
                Code = "None",
                Description = "No codes reported",
                RawResponse = response
            });
        }

        RefreshReport();
    }

    private async Task ReadReadinessAsync()
    {
        EnsureConnected();
        var response = await Transport.SendCommandAsync("0101", timeoutMs: 5000);
        _lastReadiness = ObdDecoder.DecodeReadiness(response);

        MonitorRows.Clear();
        foreach (var monitor in _lastReadiness.Monitors)
        {
            MonitorRows.Add(monitor);
        }

        MilTextBlock.Text = $"MIL: {(_lastReadiness.MilOn ? "On" : "Off")}";
        DtcCountTextBlock.Text = $"DTC count: {_lastReadiness.ConfirmedDtcCount}";
        EngineTypeTextBlock.Text = _lastReadiness.EngineType;
        AppendTerminal($"> 0101{Environment.NewLine}{response}");
        RefreshReport();
    }

    private async Task LoadSupportedPidsAsync()
    {
        EnsureConnected();
        _supportedPids.Clear();

        foreach (var command in new[] { "0100", "0120", "0140" })
        {
            var response = await Transport.SendCommandAsync(command);
            var basePid = Convert.ToInt32(command[2..], 16);
            foreach (var supportedPid in ObdDecoder.DecodeSupportedPids(response, basePid))
            {
                _supportedPids.Add(supportedPid);
            }

            AppendTerminal($"> {command}{Environment.NewLine}{response}");
        }

        SeedLivePidGrid();
    }

    private async Task ReadLiveDataOnceAsync()
    {
        EnsureConnected();
        if (_supportedPids.Count == 0)
        {
            await LoadSupportedPidsAsync();
        }

        LivePidRows.Clear();
        foreach (var definition in ObdDecoder.LivePidDefinitions)
        {
            var pidNumber = Convert.ToInt32(definition.Pid[2..], 16);
            var supported = _supportedPids.Count == 0 || _supportedPids.Contains(pidNumber);
            if (!supported)
            {
                LivePidRows.Add(new LivePidReading
                {
                    Pid = definition.Pid,
                    Name = definition.Name,
                    Unit = definition.Unit,
                    Supported = "No",
                    Value = ""
                });
                continue;
            }

            var response = await Transport.SendCommandAsync(definition.Pid);
            LivePidRows.Add(ObdDecoder.DecodeLivePid(definition, response, supported: true));
        }
    }

    private async Task ReadMode6Async()
    {
        EnsureConnected();
        foreach (var command in new[] { "0600", "0620", "0640", "0660", "0680", "06A0", "06C0", "06E0" })
        {
            var response = await Transport.SendCommandAsync(command, timeoutMs: 6000);
            AppendMode6($"> {command}{Environment.NewLine}{response}");
        }
    }

    private async Task ProbeModulesAsync()
    {
        EnsureConnected();
        AppendMode6("Read-only CAN OBD probe started.");

        try
        {
            foreach (var command in new[] { "ATH1", "ATS1", "ATSH7DF" })
            {
                var response = await Transport.SendCommandAsync(command);
                AppendMode6($"> {command}{Environment.NewLine}{response}");
            }

            foreach (var command in new[] { "0100", "0101", "0902" })
            {
                var response = await Transport.SendCommandAsync(command, timeoutMs: 6000);
                AppendMode6($"> 7DF {command}{Environment.NewLine}{response}");
            }

            for (var module = 0; module <= 7; module++)
            {
                var header = $"ATSH7E{module:X1}";
                var headerResponse = await Transport.SendCommandAsync(header);
                var probeResponse = await Transport.SendCommandAsync("0100", timeoutMs: 3000);
                AppendMode6($"> {header}{Environment.NewLine}{headerResponse}{Environment.NewLine}> 0100{Environment.NewLine}{probeResponse}");
            }
        }
        finally
        {
            await Transport.SendCommandAsync("ATSH7DF");
            await Transport.SendCommandAsync("ATS0");
            await Transport.SendCommandAsync("ATH0");
            AppendMode6("Probe finished. Header display restored off.");
        }
    }

    private async Task PollLiveDataAsync(int intervalMs, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await RunUiTaskAsync(ReadLiveDataOnceAsync, showErrors: false);
                await Task.Delay(intervalMs, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _pollingCancellation?.Dispose();
            _pollingCancellation = null;
            SetFooter("Live polling stopped.");
        }
    }

    private async Task RunUiTaskAsync(Func<Task> action, bool showErrors = true)
    {
        try
        {
            SetBusy(true);
            await action();
            SetFooter("Ready");
        }
        catch (Exception ex)
        {
            SetFooter(ex.Message);
            if (showErrors)
            {
                MessageBox.Show(this, ex.Message, "TacomaDiag", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RefreshPorts()
    {
        var ports = Elm327Client.GetSerialPorts();
        PortComboBox.ItemsSource = ports;
        if (ports.Length > 0)
        {
            PortComboBox.SelectedIndex = 0;
        }

        J2534Devices.Clear();
        foreach (var device in J2534DeviceDiscovery.FindInstalledDevices())
        {
            J2534Devices.Add(device);
        }

        J2534DllComboBox.ItemsSource = J2534Devices;
        if (J2534Devices.Count > 0 && J2534DllComboBox.SelectedIndex < 0)
        {
            J2534DllComboBox.SelectedIndex = 0;
        }

        var serialStatus = ports.Length == 0 ? "No COM ports found" : $"Found {ports.Length} COM port(s)";
        var j2534Status = J2534Devices.Count == 0 ? "no J2534 DLLs found" : $"{J2534Devices.Count} J2534 DLL(s) found";
        SetFooter($"{serialStatus}; {j2534Status}.");
    }

    private void DisconnectTransports()
    {
        _elm.Disconnect();
        _j2534.Disconnect();
        _transport = _elm;
    }

    private void UpdateAdapterControlState()
    {
        var selectedMode = AdapterModeComboBox.SelectedItem?.ToString() ?? "Serial ELM327";
        var serialEnabled = selectedMode == "Serial ELM327";
        var j2534Enabled = selectedMode == "J2534 PassThru";

        PortComboBox.IsEnabled = serialEnabled;
        BaudComboBox.IsEnabled = serialEnabled;
        J2534DllComboBox.IsEnabled = j2534Enabled;
        BrowseJ2534Button.IsEnabled = j2534Enabled;
    }

    private void SeedLivePidGrid()
    {
        LivePidRows.Clear();
        foreach (var definition in ObdDecoder.LivePidDefinitions)
        {
            var pidNumber = Convert.ToInt32(definition.Pid[2..], 16);
            LivePidRows.Add(new LivePidReading
            {
                Pid = definition.Pid,
                Name = definition.Name,
                Unit = definition.Unit,
                Supported = _supportedPids.Count == 0 ? "Unknown" : _supportedPids.Contains(pidNumber) ? "Yes" : "No",
                Value = ""
            });
        }
    }

    private void StopPolling()
    {
        _pollingCancellation?.Cancel();
    }

    private void EnsureConnected()
    {
        if (!Transport.IsConnected)
        {
            throw new InvalidOperationException("Connect to an ELM327 adapter first.");
        }
    }

    private void SetBusy(bool busy)
    {
        ConnectButton.IsEnabled = !busy;
        RefreshPortsButton.IsEnabled = !busy;
        Cursor = busy ? System.Windows.Input.Cursors.Wait : null;
    }

    private void UpdateConnectionStatus()
    {
        ConnectionStatusTextBlock.Text = Transport.IsConnected ? Transport.ConnectionName : "Disconnected";
    }

    private void SetFooter(string message)
    {
        FooterStatusTextBlock.Text = message;
    }

    private void AppendCodesLog(string command, string response)
    {
        CodesLogTextBox.AppendText($"> {command}{Environment.NewLine}{response}{Environment.NewLine}{Environment.NewLine}");
        CodesLogTextBox.ScrollToEnd();
    }

    private void AppendTerminal(string message)
    {
        TerminalTextBox.AppendText($"{message}{Environment.NewLine}{Environment.NewLine}");
        TerminalTextBox.ScrollToEnd();
    }

    private void AppendMode6(string message)
    {
        Mode6TextBox.AppendText($"{message}{Environment.NewLine}{Environment.NewLine}");
        Mode6TextBox.ScrollToEnd();
    }

    private void RefreshReport()
    {
        ReportTextBox.Text = ObdDecoder.BuildQuickReport(Transport.ConnectionName, _lastProtocol, _lastVin, DtcRows.Where(row => row.Code != "None"), _lastReadiness);
    }
}
