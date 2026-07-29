using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using TacomaDiag.Models;
using TacomaDiag.Services;

namespace TacomaDiag;

public partial class MainWindow : Window
{
    private const string SerialAdapterMode = "Serial/Bluetooth ELM327";
    private const string J2534AdapterMode = "J2534 PassThru";
    private const string DemoAdapterMode = "Demo";

    private readonly Elm327Client _elm = new();
    private readonly J2534Client _j2534 = new();
    private readonly DiagnosticHistoryStore _historyStore = new();
    private readonly HashSet<int> _supportedPids = [];
    private readonly List<LiveRecordingFrame> _liveRecordingFrames = [];
    private IObdTransport? _transport;
    private CancellationTokenSource? _pollingCancellation;
    private ReadinessSnapshot? _lastReadiness;
    private VehicleProfile _profile = VehicleProfile.ToyotaTacoma2008Base2TrFe;
    private DiagnosticWorkflow? _selectedWorkflow;
    private MppsToolInfo? _selectedMppsTool;
    private IReadOnlyList<MppsUsbDeviceInfo> _mppsUsbDevices = [];
    private IReadOnlyList<UsbSerialDeviceInfo> _knownSerialDevices = [];
    private IReadOnlyList<UsbSerialDeviceInfo> _ch340Devices = [];
    private bool _isRecordingLiveData;
    private DateTime? _recordingStartedAt;
    private string _lastProtocol = "";
    private string _lastVin = "";

    public ObservableCollection<DiagnosticTroubleCode> DtcRows { get; } = [];
    public ObservableCollection<MonitorStatus> MonitorRows { get; } = [];
    public ObservableCollection<LivePidReading> LivePidRows { get; } = [];
    public ObservableCollection<J2534DeviceInfo> J2534Devices { get; } = [];
    public ObservableCollection<FreezeFrameReading> FreezeFrameRows { get; } = [];
    public ObservableCollection<HealthFinding> HealthFindingRows { get; } = [];
    public ObservableCollection<DiagnosticSession> SessionRows { get; } = [];
    public ObservableCollection<Mode6TestResult> Mode6Rows { get; } = [];
    public ObservableCollection<MppsToolInfo> MppsTools { get; } = [];
    public ObservableCollection<ModuleScanResult> ModuleScanRows { get; } = [];
    public ObservableCollection<AdapterWizardStep> AdapterWizardRows { get; } = [];

    private IObdTransport Transport => _transport ?? _elm;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _transport = _elm;
        VehicleProfileComboBox.ItemsSource = VehicleProfile.BuiltInProfiles;
        VehicleProfileComboBox.SelectedItem = _profile;
        UpdateProfileText();
        AdapterModeComboBox.ItemsSource = new[] { SerialAdapterMode, J2534AdapterMode, DemoAdapterMode };
        AdapterModeComboBox.SelectedIndex = 0;
        ProtocolComboBox.ItemsSource = AdapterProfile.ElmProtocols;
        ProtocolComboBox.SelectedIndex = 0;
        WorkflowComboBox.ItemsSource = DiagnosticWorkflowCatalog.Workflows;
        WorkflowComboBox.SelectedIndex = 0;
        BaudComboBox.ItemsSource = new[] { 9600, 38400, 115200, 500000 };
        BaudComboBox.SelectedItem = 38400;
        RefreshPorts();
        RefreshMppsTools();
        RefreshHistory();
        SeedLivePidGrid();
        LoadSelectedWorkflow();
        UpdateRecordingStatus();
        RefreshAdvisor();
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

    private void VehicleProfileComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (VehicleProfileComboBox.SelectedItem is not VehicleProfile profile)
        {
            return;
        }

        _profile = profile;
        UpdateProfileText();
        ApplyRecommendedProtocolForProfile();
        RefreshReport();
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
        AdapterModeComboBox.SelectedItem = J2534AdapterMode;
    }

    private void MppsToolComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (MppsToolComboBox.SelectedItem is MppsToolInfo tool)
        {
            SelectMppsTool(tool);
        }
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(async () =>
        {
            DisconnectTransports();

            var selectedMode = AdapterModeComboBox.SelectedItem?.ToString() ?? SerialAdapterMode;
            if (selectedMode == DemoAdapterMode)
            {
                _elm.ConnectDemo();
                _transport = _elm;
            }
            else if (selectedMode == J2534AdapterMode)
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
            await ApplySelectedProtocolAsync();
            await ReadProtocolAsync();
            AppendTerminal("Connected.");
            AppendElmUsbAdapterGuidance();
        });
    }

    private async void AdapterSelfTestButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(AdapterSelfTestAsync);
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

    private async void FreezeFrameButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(ReadFreezeFrameAsync);
    }

    private void RefreshAdvisorButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshAdvisor();
    }

    private void SaveSessionButton_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentSession();
    }

    private void OpenHistoryFolderButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_historyStore.AppDataDirectory);
        Process.Start(new ProcessStartInfo(_historyStore.AppDataDirectory) { UseShellExecute = true });
    }

    private void RefreshHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshHistory();
    }

    private void LoadWorkflowButton_Click(object sender, RoutedEventArgs e)
    {
        LoadSelectedWorkflow();
    }

    private void CopyWorkflowToReportButton_Click(object sender, RoutedEventArgs e)
    {
        LoadSelectedWorkflow();
        RefreshReport();
        if (!string.IsNullOrWhiteSpace(WorkflowTextBox.Text))
        {
            ReportTextBox.AppendText(Environment.NewLine + "Guided Workflow" + Environment.NewLine);
            ReportTextBox.AppendText(WorkflowTextBox.Text);
        }
    }

    private void RefreshAdapterHardwareButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshPorts();
        RefreshAdapterWizardHardwareSummary();
    }

    private async void RunAdapterWizardButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(RunAdapterWizardAsync);
    }

    private async void RunHsMsSwitchCheckButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(RunHsMsSwitchCheckAsync);
    }

    private void SaveAdapterWizardReportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save adapter wizard report",
            Filter = "Text report (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"adapter-wizard-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog(this) == true)
        {
            File.WriteAllText(dialog.FileName, BuildAdapterWizardReport());
            SetFooter($"Adapter wizard report saved: {dialog.FileName}");
        }
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

    private void StartRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        _liveRecordingFrames.Clear();
        _isRecordingLiveData = true;
        _recordingStartedAt = DateTime.Now;
        UpdateRecordingStatus();
        SetFooter("Live recording started.");
    }

    private void StopRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        _isRecordingLiveData = false;
        UpdateRecordingStatus();
        SetFooter($"Live recording stopped with {_liveRecordingFrames.Count} frame(s).");
        RefreshReport();
    }

    private void ExportRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_liveRecordingFrames.Count == 0)
        {
            MessageBox.Show(this, "No live-data recording frames are available yet.", "TacomaDiag", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var reportDirectory = GetReportDirectory();
        var path = Path.Combine(reportDirectory, $"TacomaDiag-LiveData-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        File.WriteAllText(path, ReportExportService.BuildLiveRecordingCsv(_liveRecordingFrames));
        SetFooter($"Live recording exported: {path}");
        MessageBox.Show(this, path, "CSV exported", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void Mode6Button_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(ReadMode6Async);
    }

    private async void ProbeModulesButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(ProbeModulesAsync);
    }

    private async void EnhancedModuleScanButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(() => EnhancedModuleScanAsync(transmissionOnly: false));
    }

    private async void TransmissionModuleScanButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(() => EnhancedModuleScanAsync(transmissionOnly: true));
    }

    private void ScanMppsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshMppsTools();
    }

    private void BrowseMppsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select MPPS V16 executable",
            Filter = "MPPS executable (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var manualTool = MppsToolDiscovery.FromManualPath(dialog.FileName);
        MppsTools.Add(manualTool);
        MppsToolComboBox.SelectedItem = manualTool;
        SelectMppsTool(manualTool);
    }

    private void LaunchMppsButton_Click(object sender, RoutedEventArgs e)
    {
        var tool = GetSelectedMppsTool();
        if (tool is null || !File.Exists(tool.ExecutablePath))
        {
            MessageBox.Show(this, "Select a valid MPPS executable first.", "TacomaDiag", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(
            this,
            "Launch MPPS V16? ECU read/write operations happen inside MPPS. Keep stable battery support connected and save original ECU files before writing.",
            "Launch MPPS V16",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        Process.Start(new ProcessStartInfo(tool.ExecutablePath)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(tool.ExecutablePath) ?? ""
        });

        SetFooter($"Launched MPPS: {tool.ExecutablePath}");
    }

    private void AddMppsToReportButton_Click(object sender, RoutedEventArgs e)
    {
        SelectMppsTool(GetSelectedMppsTool());
        RefreshReport();
        ReportTextBox.AppendText(Environment.NewLine + MppsNotesTextBox.Text + Environment.NewLine);
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
        var reportDirectory = GetReportDirectory();

        var fileName = $"TacomaDiag-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
        var path = Path.Combine(reportDirectory, fileName);
        File.WriteAllText(path, ReportTextBox.Text);

        SetFooter($"Report saved: {path}");
        MessageBox.Show(this, path, "Report saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveHtmlReportButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshReport();
        var reportDirectory = GetReportDirectory();
        var path = Path.Combine(reportDirectory, $"TacomaDiag-{DateTime.Now:yyyyMMdd-HHmmss}.html");
        File.WriteAllText(path, ReportExportService.BuildHtmlReport(ReportTextBox.Text, HealthFindingRows, _liveRecordingFrames));
        SetFooter($"HTML report saved: {path}");
        MessageBox.Show(this, path, "HTML report saved", MessageBoxButton.OK, MessageBoxImage.Information);
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

    private async Task ApplySelectedProtocolAsync()
    {
        if (AdapterModeComboBox.SelectedItem?.ToString() != SerialAdapterMode)
        {
            return;
        }

        if (ProtocolComboBox.SelectedItem is not AdapterProfile protocol)
        {
            return;
        }

        var response = await Transport.SendCommandAsync(protocol.Command, timeoutMs: 3000);
        AppendTerminal($"> {protocol.Command} ({protocol.Name}){Environment.NewLine}{response}");
        if (!string.IsNullOrWhiteSpace(protocol.Description))
        {
            AppendTerminal(protocol.Description);
        }
        if (!string.IsNullOrWhiteSpace(protocol.SwitchPosition))
        {
            AppendTerminal(protocol.SwitchPosition);
        }
    }

    private async Task AdapterSelfTestAsync()
    {
        EnsureConnected();
        AppendElmUsbAdapterGuidance();
        foreach (var command in new[] { "ATI", "AT@1", "ATDP", "ATDPN", "0100" })
        {
            var response = await Transport.SendCommandAsync(command, timeoutMs: command == "0100" ? 6500 : 3000);
            AppendTerminal($"> {command}{Environment.NewLine}{response}");
        }
    }

    private async Task RunAdapterWizardAsync()
    {
        AdapterWizardRows.Clear();
        AdapterWizardLogTextBox.Clear();
        RefreshPorts();
        AddAdapterWizardStep("Hardware refresh", "Done", BuildAdapterHardwareSummary(), "");

        var connectionDetail = await EnsureWizardTransportConnectedAsync();
        AddAdapterWizardStep("Open adapter", "Pass", connectionDetail, Transport.ConnectionName);

        foreach (var command in new[] { "ATZ", "ATI", "ATE0", "ATL0", "ATS0", "ATH0" })
        {
            var response = await SendWizardCommandAsync(command, timeoutMs: command == "ATZ" ? 4500 : 3000);
            AddAdapterWizardStep(
                $"ELM {command}",
                LooksLikePositiveAdapterResponse(response) ? "Pass" : "Review",
                DescribeAdapterCommand(command, response),
                response);
        }

        await ApplySelectedProtocolAsync();
        AddAdapterWizardStep("Protocol select", "Done", GetSelectedAdapterProfile()?.Name ?? "Selected protocol applied.", "");

        var protocol = await SendWizardCommandAsync("ATDP", timeoutMs: 3000);
        _lastProtocol = protocol;
        AddAdapterWizardStep("Protocol name", string.IsNullOrWhiteSpace(protocol) ? "Review" : "Pass", protocol, protocol);

        var protocolNumber = await SendWizardCommandAsync("ATDPN", timeoutMs: 3000);
        AddAdapterWizardStep("Protocol number", string.IsNullOrWhiteSpace(protocolNumber) ? "Review" : "Pass", protocolNumber, protocolNumber);

        var vehicleConnected = WizardVehicleConnectedCheckBox.IsChecked == true;
        var pidSupport = await SendWizardCommandAsync("0100", timeoutMs: 6500);
        AddAdapterWizardStep(
            "Vehicle ECU response",
            LooksLikeObdPositiveResponse(pidSupport, "41 00") ? "Pass" : vehicleConnected ? "Fail" : "Info",
            DescribeVehicleResponse(pidSupport, vehicleConnected),
            pidSupport);

        var vinResponse = await SendWizardCommandAsync("0902", timeoutMs: 6500);
        _lastVin = ObdDecoder.DecodeVin(vinResponse);
        VinTextBlock.Text = string.IsNullOrWhiteSpace(_lastVin) ? "VIN: not read" : $"VIN: {_lastVin}";
        AddAdapterWizardStep(
            "VIN read",
            string.IsNullOrWhiteSpace(_lastVin) ? vehicleConnected ? "Review" : "Info" : "Pass",
            string.IsNullOrWhiteSpace(_lastVin) ? "VIN was not decoded from this response." : _lastVin,
            vinResponse);

        AddAdapterWizardStep(
            "Next action",
            "Ready",
            vehicleConnected
                ? "If HS-CAN works, continue with Full Scan. Use HS/MS Switch Check before Ford/Mazda body-module work."
                : "Adapter path is tested. Connect vehicle, turn ignition ON, then rerun the wizard.",
            "");

        RefreshReport();
        SetAdapterWizardStatus("Adapter wizard complete.");
    }

    private async Task RunHsMsSwitchCheckAsync()
    {
        EnsureConnected();
        AdapterWizardLogTextBox.AppendText("HS/MS switch check started." + Environment.NewLine + Environment.NewLine);

        MessageBox.Show(this, "Set the adapter's physical switch to HS-CAN, then click OK. Use ignition ON if the vehicle is connected.", "HS-CAN switch check", MessageBoxButton.OK, MessageBoxImage.Information);
        var hsProtocol = await SendWizardCommandAsync("ATSP6", timeoutMs: 3000);
        var hsResponse = await SendWizardCommandAsync("0100", timeoutMs: 6500);
        AddAdapterWizardStep("HS-CAN switch", LooksLikeObdPositiveResponse(hsResponse, "41 00") ? "Pass" : "Review", DescribeVehicleResponse(hsResponse, WizardVehicleConnectedCheckBox.IsChecked == true), $"ATSP6: {hsProtocol}{Environment.NewLine}0100: {hsResponse}");

        MessageBox.Show(this, "Set the adapter's physical switch to MS-CAN, then click OK. MS-CAN is mainly for Ford/Mazda body, cluster, HVAC, and comfort modules.", "MS-CAN switch check", MessageBoxButton.OK, MessageBoxImage.Information);
        var msProtocol = await SendWizardCommandAsync("ATSP8", timeoutMs: 3000);
        var msResponse = await SendWizardCommandAsync("0100", timeoutMs: 6500);
        AddAdapterWizardStep("MS-CAN switch", LooksLikeObdPositiveResponse(msResponse, "41 00") ? "Pass" : "Info", "MS-CAN may not answer generic OBD PID 0100 unless a compatible ECU is present on that bus.", $"ATSP8: {msProtocol}{Environment.NewLine}0100: {msResponse}");

        if (GetSelectedAdapterProfile() is { } selectedProfile)
        {
            var restoreResponse = await SendWizardCommandAsync(selectedProfile.Command, timeoutMs: 3000);
            AddAdapterWizardStep("Restore profile", "Done", $"Restored {selectedProfile.Name}.", restoreResponse);
        }

        RefreshReport();
        SetAdapterWizardStatus("HS/MS switch check complete.");
    }

    private async Task ReadVinAsync()
    {
        EnsureConnected();
        var response = await Transport.SendCommandAsync("0902", timeoutMs: 6000);
        _lastVin = ObdDecoder.DecodeVin(response);
        VinTextBlock.Text = string.IsNullOrWhiteSpace(_lastVin) ? "VIN: not read" : $"VIN: {_lastVin}";
        AppendTerminal($"> 0902{Environment.NewLine}{response}");
        RefreshReport();
        RefreshAdvisor();
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
        RefreshAdvisor();
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
        RefreshAdvisor();
    }

    private async Task ReadFreezeFrameAsync()
    {
        EnsureConnected();
        FreezeFrameRows.Clear();
        FreezeFrameLogTextBox.Clear();

        foreach (var definition in ObdDecoder.LivePidDefinitions.Take(12))
        {
            var command = "02" + definition.Pid[2..];
            var response = await Transport.SendCommandAsync(command, timeoutMs: 5000);
            FreezeFrameLogTextBox.AppendText($"> {command}{Environment.NewLine}{response}{Environment.NewLine}{Environment.NewLine}");
            var reading = ObdDecoder.DecodeFreezeFramePid(definition, response);
            if (reading.Value != "No data")
            {
                FreezeFrameRows.Add(reading);
            }
        }

        FreezeFrameLogTextBox.ScrollToEnd();
        RefreshAdvisor();
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

        LiveDashboardTextBox.Text = BuildLiveDashboard();
        CaptureLiveRecordingFrame();
        RefreshAdvisor();
    }

    private async Task ReadMode6Async()
    {
        EnsureConnected();
        Mode6Rows.Clear();
        Mode6TextBox.Clear();
        foreach (var command in new[] { "0600", "0620", "0640", "0660", "0680", "06A0", "06C0", "06E0" })
        {
            var response = await Transport.SendCommandAsync(command, timeoutMs: 6000);
            AppendMode6($"> {command}{Environment.NewLine}{response}");
            foreach (var row in ObdDecoder.DecodeMode6Response(response))
            {
                Mode6Rows.Add(row);
            }
        }

        RefreshReport();
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

    private async Task EnhancedModuleScanAsync(bool transmissionOnly)
    {
        EnsureConnected();

        var targets = ManufacturerModuleCatalog.GetTargets(_profile);
        if (targets.Count == 0)
        {
            MessageBox.Show(this, "Enhanced module targets are not defined for the selected vehicle profile yet.", "TacomaDiag", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var selectedTargets = targets
            .Where(target => !transmissionOnly || target.System.Equals("Transmission", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (!transmissionOnly)
        {
            ModuleScanRows.Clear();
            AppendMode6($"Read-only enhanced module scan started for {_profile.Name}.");
        }
        else
        {
            AppendMode6($"Read-only transmission module scan started for {_profile.Name}.");
        }

        try
        {
            foreach (var command in new[] { "ATH1", "ATS1", "ATCAF1", "ATSP6", "ATAT2", "ATSTFF" })
            {
                var response = await Transport.SendCommandAsync(command, timeoutMs: 3000);
                AppendMode6($"> {command}{Environment.NewLine}{response}");
            }

            foreach (var target in selectedTargets)
            {
                var scan = await ScanModuleTargetAsync(target);
                ModuleScanRows.Add(scan);
            }
        }
        finally
        {
            await Transport.SendCommandAsync("ATSH7DF", timeoutMs: 3000);
            await Transport.SendCommandAsync("ATH0", timeoutMs: 3000);
            await Transport.SendCommandAsync("ATS0", timeoutMs: 3000);
            AppendMode6("Enhanced module scan finished. Header display restored off.");
        }

        RefreshReport();
    }

    private async Task<ModuleScanResult> ScanModuleTargetAsync(VehicleModuleTarget target)
    {
        var headerResponse = await Transport.SendCommandAsync($"ATSH{target.RequestHeader}", timeoutMs: 3000);
        AppendMode6($"> ATSH{target.RequestHeader}{Environment.NewLine}{headerResponse}");

        var dtcResponse = await Transport.SendCommandAsync("1902FF", timeoutMs: 6500);
        AppendMode6($"> {target.RequestHeader} 1902FF{Environment.NewLine}{dtcResponse}");
        var dtcs = ObdDecoder.DecodeUdsDtcResponse(dtcResponse);

        var idResponse = await Transport.SendCommandAsync("22F190", timeoutMs: 5000);
        AppendMode6($"> {target.RequestHeader} 22F190{Environment.NewLine}{idResponse}");
        var ecuId = ObdDecoder.DecodeAsciiFromPositiveResponse(idResponse, 0x62, 0xF1, 0x90);

        if (string.IsNullOrWhiteSpace(ecuId))
        {
            var partResponse = await Transport.SendCommandAsync("22F187", timeoutMs: 5000);
            AppendMode6($"> {target.RequestHeader} 22F187{Environment.NewLine}{partResponse}");
            ecuId = ObdDecoder.DecodeAsciiFromPositiveResponse(partResponse, 0x62, 0xF1, 0x87);
            idResponse += Environment.NewLine + partResponse;
        }

        var status = dtcs.Count > 0 || !LooksLikeNoData(dtcResponse) || !string.IsNullOrWhiteSpace(ecuId)
            ? "Responded"
            : "No response";

        var dtcSummary = dtcs.Count == 0
            ? LooksLikeNoData(dtcResponse) ? "No response / unsupported" : "No DTC records decoded"
            : string.Join("; ", dtcs.Select(dtc => $"{dtc.Code} status {dtc.Status}"));

        return new ModuleScanResult
        {
            Module = target.Name,
            System = target.System,
            RequestHeader = target.RequestHeader,
            Status = status,
            DtcSummary = dtcSummary,
            EcuId = ecuId,
            RawResponse = dtcResponse + Environment.NewLine + idResponse
        };
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
        _knownSerialDevices = UsbSerialDeviceDiscovery.FindKnownAdapterDevices();
        _ch340Devices = UsbSerialDeviceDiscovery.FindCh340Devices();
        PortComboBox.ItemsSource = ports;
        if (ports.Length > 0)
        {
            var preferredPort = _knownSerialDevices
                .Select(device => device.PortName)
                .FirstOrDefault(port => !string.IsNullOrWhiteSpace(port) && ports.Contains(port, StringComparer.OrdinalIgnoreCase));
            PortComboBox.SelectedItem = preferredPort ?? ports[0];
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
        var adapterStatus = _knownSerialDevices.Count == 0 ? "no known USB/Bluetooth serial OBD adapter detected" : $"{_knownSerialDevices.Count} USB/Bluetooth serial adapter candidate(s) detected";
        SetFooter($"{serialStatus}; {j2534Status}; {adapterStatus}.");
        SetAdapterWizardStatus($"{serialStatus}; {adapterStatus}.");
        RefreshReport();
    }

    private void RefreshMppsTools()
    {
        _mppsUsbDevices = MppsToolDiscovery.FindConnectedUsbDevices();
        MppsTools.Clear();
        foreach (var tool in MppsToolDiscovery.FindInstalledTools())
        {
            MppsTools.Add(tool);
        }

        var usbStatus = _mppsUsbDevices.Count == 0
            ? "no MPPS USB device detected"
            : $"{_mppsUsbDevices.Count} MPPS USB device(s) detected: {string.Join("; ", _mppsUsbDevices.Select(device => device.DriverSummary))}";

        MppsToolComboBox.ItemsSource = MppsTools;
        if (MppsTools.Count > 0)
        {
            MppsToolComboBox.SelectedIndex = 0;
            SelectMppsTool(MppsTools[0]);
            SetFooter($"Found {MppsTools.Count} MPPS tool candidate(s); {usbStatus}.");
        }
        else
        {
            SelectMppsTool(null);
            SetFooter($"No MPPS executable found automatically; {usbStatus}. Use Browse EXE if MPPS is installed.");
        }
    }

    private void DisconnectTransports()
    {
        _elm.Disconnect();
        _j2534.Disconnect();
        _transport = _elm;
    }

    private void UpdateAdapterControlState()
    {
        var selectedMode = AdapterModeComboBox.SelectedItem?.ToString() ?? SerialAdapterMode;
        var serialEnabled = selectedMode == SerialAdapterMode;
        var j2534Enabled = selectedMode == J2534AdapterMode;

        PortComboBox.IsEnabled = serialEnabled;
        BaudComboBox.IsEnabled = serialEnabled;
        J2534DllComboBox.IsEnabled = j2534Enabled;
        BrowseJ2534Button.IsEnabled = j2534Enabled;
        ProtocolComboBox.IsEnabled = serialEnabled;
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
        RefreshAdapterHardwareButton.IsEnabled = !busy;
        RunAdapterWizardButton.IsEnabled = !busy;
        RunHsMsSwitchCheckButton.IsEnabled = !busy;
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

    private void AddAdapterWizardStep(string step, string status, string detail, string rawResponse)
    {
        var row = new AdapterWizardStep
        {
            Step = step,
            Status = status,
            Detail = string.IsNullOrWhiteSpace(detail) ? "(no detail)" : detail,
            RawResponse = rawResponse
        };

        AdapterWizardRows.Add(row);
        AdapterWizardLogTextBox.AppendText($"[{row.Status}] {row.Step}: {row.Detail}{Environment.NewLine}");
        if (!string.IsNullOrWhiteSpace(row.RawResponse))
        {
            AdapterWizardLogTextBox.AppendText(row.RawResponse + Environment.NewLine);
        }

        AdapterWizardLogTextBox.AppendText(Environment.NewLine);
        AdapterWizardLogTextBox.ScrollToEnd();
    }

    private void SetAdapterWizardStatus(string message)
    {
        if (AdapterWizardStatusTextBlock is not null)
        {
            AdapterWizardStatusTextBlock.Text = message;
        }
    }

    private void AppendElmUsbAdapterGuidance()
    {
        if (AdapterModeComboBox.SelectedItem?.ToString() != SerialAdapterMode)
        {
            return;
        }

        if (_ch340Devices.Count > 0)
        {
            AppendTerminal("Detected CH340/CH341 USB serial adapter:" + Environment.NewLine + string.Join(Environment.NewLine, _ch340Devices.Select(device => $"- {device.Name}: {device.DriverSummary}")));
        }
        else
        {
            AppendTerminal("No CH340/CH341 USB serial adapter was detected by Windows PnP. If this is your USB ELM327 HS/MS-CAN adapter, install/repair the CH340T driver and click Refresh.");
        }

        if (GetSelectedAdapterProfile() is { } profile)
        {
            AppendTerminal($"Selected adapter/protocol profile: {profile.Name}{Environment.NewLine}{profile.Description}");
            if (!string.IsNullOrWhiteSpace(profile.SwitchPosition))
            {
                AppendTerminal(profile.SwitchPosition);
            }
        }
    }

    private void AppendMode6(string message)
    {
        Mode6TextBox.AppendText($"{message}{Environment.NewLine}{Environment.NewLine}");
        Mode6TextBox.ScrollToEnd();
    }

    private void RefreshAdapterWizardHardwareSummary()
    {
        AdapterWizardRows.Clear();
        AdapterWizardLogTextBox.Clear();
        AddAdapterWizardStep("Hardware refresh", "Done", BuildAdapterHardwareSummary(), "");
        SetAdapterWizardStatus(BuildAdapterHardwareSummary());
    }

    private async Task<string> EnsureWizardTransportConnectedAsync()
    {
        if (Transport.IsConnected)
        {
            return $"Already connected through {Transport.ConnectionName}.";
        }

        DisconnectTransports();

        var selectedMode = AdapterModeComboBox.SelectedItem?.ToString() ?? SerialAdapterMode;
        if (selectedMode == DemoAdapterMode)
        {
            _elm.ConnectDemo();
            _transport = _elm;
            UpdateConnectionStatus();
            await Task.CompletedTask;
            return "Demo adapter opened.";
        }

        if (selectedMode == J2534AdapterMode)
        {
            if (J2534DllComboBox.SelectedItem is not J2534DeviceInfo j2534Device)
            {
                throw new InvalidOperationException("Select a J2534 DLL or use Browse DLL before running the adapter wizard.");
            }

            _j2534.Connect(j2534Device.FunctionLibrary);
            _transport = _j2534;
            UpdateConnectionStatus();
            return $"J2534 DLL opened: {j2534Device.FunctionLibrary}";
        }

        var portName = PortComboBox.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(portName))
        {
            throw new InvalidOperationException("Select a COM port before running the adapter wizard.");
        }

        var baudRate = BaudComboBox.SelectedItem is int selectedBaud ? selectedBaud : 38400;
        _elm.ConnectSerial(portName, baudRate);
        _transport = _elm;
        UpdateConnectionStatus();
        return $"Serial port opened: {portName} @ {baudRate}.";
    }

    private async Task<string> SendWizardCommandAsync(string command, int timeoutMs)
    {
        try
        {
            var response = await Transport.SendCommandAsync(command, timeoutMs);
            AppendTerminal($"> {command}{Environment.NewLine}{response}");
            return response;
        }
        catch (Exception ex)
        {
            var response = $"ERROR: {ex.Message}";
            AppendTerminal($"> {command}{Environment.NewLine}{response}");
            return response;
        }
    }

    private string BuildAdapterHardwareSummary()
    {
        var ports = Elm327Client.GetSerialPorts();
        var portSummary = ports.Length == 0 ? "No COM ports visible." : $"COM ports: {string.Join(", ", ports)}.";
        var serialSummary = _knownSerialDevices.Count == 0
            ? "No known USB/Bluetooth serial adapter candidate detected."
            : "Adapter candidates: " + string.Join("; ", _knownSerialDevices.Select(device => $"{device.AdapterFamily} {device.Name} {device.DriverSummary}"));
        var j2534Summary = J2534Devices.Count == 0
            ? "No registered J2534 DLLs found."
            : $"J2534 DLLs: {J2534Devices.Count}.";

        return $"{portSummary} {serialSummary} {j2534Summary}";
    }

    private string BuildAdapterWizardReport()
    {
        using var writer = new StringWriter();
        writer.WriteLine("OBD2 Master, Custom - Adapter Wizard Report");
        writer.WriteLine($"Generated: {DateTime.Now:G}");
        writer.WriteLine($"Vehicle profile: {_profile.Name}");
        writer.WriteLine($"Connection: {Transport.ConnectionName}");
        writer.WriteLine($"Selected adapter mode: {AdapterModeComboBox.SelectedItem}");
        writer.WriteLine($"Selected protocol profile: {GetSelectedAdapterProfile()?.Name ?? "(none)"}");
        writer.WriteLine($"Vehicle connected box: {WizardVehicleConnectedCheckBox.IsChecked == true}");
        writer.WriteLine();
        writer.WriteLine("Hardware");
        writer.WriteLine(BuildAdapterHardwareSummary());
        foreach (var device in _knownSerialDevices)
        {
            writer.WriteLine($"- {device.AdapterFamily}: {device.Name}; {device.DriverSummary}; {device.PnpDeviceId}");
        }

        writer.WriteLine();
        writer.WriteLine("Steps");
        foreach (var row in AdapterWizardRows)
        {
            writer.WriteLine($"[{row.Status}] {row.Step}: {row.Detail}");
            if (!string.IsNullOrWhiteSpace(row.RawResponse))
            {
                writer.WriteLine(row.RawResponse);
            }
        }

        writer.WriteLine();
        writer.WriteLine("Raw Log");
        writer.WriteLine(AdapterWizardLogTextBox.Text);
        return writer.ToString();
    }

    private static bool LooksLikePositiveAdapterResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        return !LooksLikeNoData(response) &&
            !response.Contains("ERROR", StringComparison.OrdinalIgnoreCase) &&
            !response.Trim().Equals("?", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeObdPositiveResponse(string response, string expectedPrefix)
    {
        var compactResponse = response.Replace(" ", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal)
            .ToUpperInvariant();
        var compactPrefix = expectedPrefix.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        return compactResponse.Contains(compactPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeAdapterCommand(string command, string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return $"{command} returned no text. Some clones are quiet, but this may indicate a timeout.";
        }

        if (response.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return $"{command} failed. Check port, baud rate, driver, and whether another app has the port open.";
        }

        return command switch
        {
            "ATI" => $"Adapter identity: {response}",
            "ATZ" => $"Adapter reset response: {response}",
            _ => response
        };
    }

    private static string DescribeVehicleResponse(string response, bool vehicleConnected)
    {
        if (LooksLikeObdPositiveResponse(response, "41 00"))
        {
            return "Vehicle ECU answered generic OBD PID support request.";
        }

        if (!vehicleConnected)
        {
            return "No generic ECU response. This is expected when the vehicle is not connected or ignition is off.";
        }

        if (LooksLikeNoData(response))
        {
            return "No ECU response. Check ignition ON, selected COM port, baud rate, protocol, and HS/MS switch position.";
        }

        return "Unexpected response. Save the wizard report and review raw data.";
    }

    private void RefreshReport()
    {
        ReportTextBox.Text = ObdDecoder.BuildQuickReport(Transport.ConnectionName, _lastProtocol, _lastVin, DtcRows.Where(row => row.Code != "None"), _lastReadiness);
        if (FreezeFrameRows.Count > 0)
        {
            ReportTextBox.AppendText(Environment.NewLine + "Freeze Frame" + Environment.NewLine);
            foreach (var row in FreezeFrameRows)
            {
                ReportTextBox.AppendText($"{row.Name}: {row.Value} {row.Unit}{Environment.NewLine}");
            }
        }

        if (Mode6Rows.Count > 0)
        {
            ReportTextBox.AppendText(Environment.NewLine + "Mode 6 Monitor Tests" + Environment.NewLine);
            foreach (var row in Mode6Rows.Take(40))
            {
                ReportTextBox.AppendText($"{row.TestId} {row.ComponentId}: value {row.Value}, min {row.Minimum}, max {row.Maximum}, status {row.Status}{Environment.NewLine}");
            }
        }

        if (_liveRecordingFrames.Count > 0)
        {
            ReportTextBox.AppendText(Environment.NewLine + "Live Recording" + Environment.NewLine);
            ReportTextBox.AppendText($"Frames: {_liveRecordingFrames.Count}{Environment.NewLine}");
            ReportTextBox.AppendText($"Started: {_recordingStartedAt:G}{Environment.NewLine}");
            var last = _liveRecordingFrames[^1];
            ReportTextBox.AppendText($"Last frame: RPM {last.Rpm}, speed {last.Speed}, coolant {last.Coolant}, STFT {last.ShortTermFuelTrim}, LTFT {last.LongTermFuelTrim}, voltage {last.Voltage}{Environment.NewLine}");
        }

        if (_selectedWorkflow is not null)
        {
            ReportTextBox.AppendText(Environment.NewLine + "Selected Workflow" + Environment.NewLine);
            ReportTextBox.AppendText($"{_selectedWorkflow.Name}: {_selectedWorkflow.Objective}{Environment.NewLine}");
        }

        if (ModuleScanRows.Count > 0)
        {
            ReportTextBox.AppendText(Environment.NewLine + "Enhanced Module Scan" + Environment.NewLine);
            foreach (var row in ModuleScanRows)
            {
                ReportTextBox.AppendText($"{row.Module} ({row.RequestHeader}): {row.Status}; {row.DtcSummary}; ECU ID {row.EcuId}{Environment.NewLine}");
            }
        }

        if (_knownSerialDevices.Count > 0 || _ch340Devices.Count > 0 || GetSelectedAdapterProfile() is { AdapterFamily.Length: > 0 })
        {
            ReportTextBox.AppendText(Environment.NewLine + "ELM327 USB HS/MS-CAN Adapter" + Environment.NewLine);
            if (GetSelectedAdapterProfile() is { } selectedProfile)
            {
                ReportTextBox.AppendText($"Selected adapter/protocol profile: {selectedProfile.Name}{Environment.NewLine}");
                if (!string.IsNullOrWhiteSpace(selectedProfile.SwitchPosition))
                {
                    ReportTextBox.AppendText($"Switch guidance: {selectedProfile.SwitchPosition}{Environment.NewLine}");
                }
            }

            if (_knownSerialDevices.Count == 0)
            {
                ReportTextBox.AppendText("Known USB/Bluetooth serial adapter: not detected by Windows PnP in this session." + Environment.NewLine);
            }
            else
            {
                foreach (var device in _knownSerialDevices)
                {
                    ReportTextBox.AppendText($"{device.AdapterFamily}: {device.Name}; {device.DriverSummary}; {device.PnpDeviceId}{Environment.NewLine}");
                }
            }
        }

        if (AdapterWizardRows.Count > 0)
        {
            ReportTextBox.AppendText(Environment.NewLine + "Adapter Wizard" + Environment.NewLine);
            foreach (var row in AdapterWizardRows)
            {
                ReportTextBox.AppendText($"{row.Step}: {row.Status}; {row.Detail}{Environment.NewLine}");
            }
        }

        if (_selectedMppsTool is not null || _mppsUsbDevices.Count > 0)
        {
            ReportTextBox.AppendText(Environment.NewLine + "MPPS V16 Tool" + Environment.NewLine);
            if (_selectedMppsTool is not null)
            {
                ReportTextBox.AppendText($"Executable: {_selectedMppsTool.ExecutablePath}{Environment.NewLine}");
                ReportTextBox.AppendText($"Source: {_selectedMppsTool.Source}{Environment.NewLine}");
                ReportTextBox.AppendText($"Status: {_selectedMppsTool.Status}{Environment.NewLine}");
            }
            else
            {
                ReportTextBox.AppendText($"Executable: Not selected{Environment.NewLine}");
            }

            ReportTextBox.AppendText($"J2534 DLLs registered: {J2534Devices.Count}{Environment.NewLine}");
            foreach (var device in _mppsUsbDevices)
            {
                ReportTextBox.AppendText($"USB device: {device.Name}; {device.DriverSummary}; {device.PnpDeviceId}{Environment.NewLine}");
            }
        }
    }

    private void RefreshAdvisor()
    {
        HealthFindingRows.Clear();
        foreach (var finding in DiagnosticAdvisor.BuildFindings(DtcRows, _lastReadiness, LivePidRows, FreezeFrameRows))
        {
            HealthFindingRows.Add(finding);
        }

        ReadinessGuideTextBox.Text = DiagnosticAdvisor.BuildReadinessGuide(_lastReadiness);
    }

    private void RefreshHistory()
    {
        SessionRows.Clear();
        foreach (var session in _historyStore.Load().Sessions.OrderByDescending(session => session.UpdatedAt))
        {
            SessionRows.Add(session);
        }
    }

    private void SaveCurrentSession()
    {
        var stored = DtcRows.Count(row => row.Type == "Stored" && row.Code != "None");
        var pending = DtcRows.Count(row => row.Type == "Pending" && row.Code != "None");
        var permanent = DtcRows.Count(row => row.Type == "Permanent" && row.Code != "None");
        var notReady = _lastReadiness?.Monitors.Count(monitor => monitor.Status == "Not ready") ?? 0;
        var health = HealthFindingRows.FirstOrDefault()?.Finding ?? "No findings";

        var session = new DiagnosticSession
        {
            VehicleName = _profile.Name,
            Vin = _lastVin,
            Connection = Transport.ConnectionName,
            Protocol = _lastProtocol,
            StoredCodeCount = stored,
            PendingCodeCount = pending,
            PermanentCodeCount = permanent,
            NotReadyMonitorCount = notReady,
            HealthSummary = health
        };

        session.Events.Add(new DiagnosticEvent
        {
            Category = "Session",
            Summary = "Saved diagnostic session",
            Details = ReportTextBox.Text
        });

        _historyStore.UpsertSession(session);
        RefreshHistory();
        SetFooter($"Session saved to {_historyStore.HistoryPath}");
    }

    private string BuildLiveDashboard()
    {
        string Pick(string pid)
        {
            var row = LivePidRows.FirstOrDefault(item => item.Pid == pid);
            return row is null || string.IsNullOrWhiteSpace(row.Value) ? "--" : $"{row.Value} {row.Unit}";
        }

        return $"RPM {Pick("010C")} | Speed {Pick("010D")} | Coolant {Pick("0105")} | STFT {Pick("0106")} | LTFT {Pick("0107")} | Voltage {Pick("0142")}";
    }

    private void UpdateProfileText()
    {
        ProfileTextBlock.Text = $"{_profile.Name} | {_profile.Engine} | {_profile.ExpectedProtocol}";
    }

    private void ApplyRecommendedProtocolForProfile()
    {
        if (ProtocolComboBox.ItemsSource is not IEnumerable<AdapterProfile> profiles)
        {
            return;
        }

        var preferredProfileName = _profile.ManufacturerFamily is "Ford" or "Mazda"
            ? "Ford/Mazda HS-CAN switch"
            : "";
        var canProfile = profiles.FirstOrDefault(profile => profile.Name == preferredProfileName)
            ?? profiles.FirstOrDefault(profile => profile.Command == "ATSP6");
        if (canProfile is not null && _profile.ExpectedProtocol.Contains("CAN", StringComparison.OrdinalIgnoreCase))
        {
            ProtocolComboBox.SelectedItem = canProfile;
        }
    }

    private AdapterProfile? GetSelectedAdapterProfile()
    {
        return ProtocolComboBox.SelectedItem as AdapterProfile;
    }

    private static bool LooksLikeNoData(string response)
    {
        return string.IsNullOrWhiteSpace(response) ||
            response.Contains("NO DATA", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("UNABLE", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("CAN ERROR", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("STOPPED", StringComparison.OrdinalIgnoreCase) ||
            response.Trim().Equals("?", StringComparison.OrdinalIgnoreCase);
    }

    private MppsToolInfo? GetSelectedMppsTool()
    {
        return MppsToolComboBox.SelectedItem as MppsToolInfo ?? _selectedMppsTool;
    }

    private void SelectMppsTool(MppsToolInfo? tool)
    {
        _selectedMppsTool = tool;
        MppsPathTextBox.Text = tool is null ? "No MPPS executable selected." : tool.ExecutablePath;
        MppsNotesTextBox.Text = MppsToolDiscovery.BuildSafetyChecklist(tool, _mppsUsbDevices, J2534Devices.Count);
    }

    private void LoadSelectedWorkflow()
    {
        if (WorkflowComboBox.SelectedItem is not DiagnosticWorkflow workflow)
        {
            return;
        }

        _selectedWorkflow = workflow;
        WorkflowTextBox.Text = DiagnosticWorkflowCatalog.BuildWorkflowText(workflow);
        SetFooter($"Loaded workflow: {workflow.Name}");
    }

    private void CaptureLiveRecordingFrame()
    {
        if (!_isRecordingLiveData)
        {
            return;
        }

        _liveRecordingFrames.Add(new LiveRecordingFrame
        {
            Timestamp = DateTime.Now,
            Rpm = PickLiveValue("010C"),
            Speed = PickLiveValue("010D"),
            Coolant = PickLiveValue("0105"),
            ShortTermFuelTrim = PickLiveValue("0106"),
            LongTermFuelTrim = PickLiveValue("0107"),
            Voltage = PickLiveValue("0142")
        });

        UpdateRecordingStatus();
    }

    private string PickLiveValue(string pid)
    {
        var row = LivePidRows.FirstOrDefault(item => item.Pid == pid);
        if (row is null || string.IsNullOrWhiteSpace(row.Value))
        {
            return "";
        }

        return string.IsNullOrWhiteSpace(row.Unit) ? row.Value : $"{row.Value} {row.Unit}";
    }

    private void UpdateRecordingStatus()
    {
        var state = _isRecordingLiveData ? "on" : "off";
        var started = _recordingStartedAt.HasValue ? $" since {_recordingStartedAt:T}" : "";
        LiveRecordingStatusTextBlock.Text = $"Recording: {state}; frames {_liveRecordingFrames.Count}{started}";
    }

    private static string GetReportDirectory()
    {
        var reportDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TacomaDiag Reports");
        Directory.CreateDirectory(reportDirectory);
        return reportDirectory;
    }
}
