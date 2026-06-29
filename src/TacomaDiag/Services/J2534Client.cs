using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TacomaDiag.Services;

public sealed class J2534Client : IObdTransport
{
    private const uint ProtocolIso15765 = 0x00000006;
    private const uint BaudCan500K = 500000;
    private const uint FunctionalRequestId = 0x000007DF;
    private const uint EngineRequestId = 0x000007E0;
    private const int StatusNoError = 0;
    private const uint FlowControlFilter = 0x00000003;
    private const uint Iso15765FramePad = 0x00000040;
    private const int MaxDataSize = 4128;
    private const int MaxReadMessages = 8;

    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly List<uint> _filterIds = [];

    private IntPtr _libraryHandle;
    private PassThruOpenDelegate? _open;
    private PassThruCloseDelegate? _close;
    private PassThruConnectDelegate? _connect;
    private PassThruDisconnectDelegate? _disconnect;
    private PassThruReadMsgsDelegate? _readMsgs;
    private PassThruWriteMsgsDelegate? _writeMsgs;
    private PassThruStartMsgFilterDelegate? _startMsgFilter;
    private PassThruStopMsgFilterDelegate? _stopMsgFilter;
    private PassThruReadVersionDelegate? _readVersion;
    private PassThruGetLastErrorDelegate? _getLastError;

    private uint _deviceId;
    private uint _channelId;
    private uint _currentRequestId = FunctionalRequestId;
    private bool _headersOn;
    private bool _spacesOn = true;
    private string _dllPath = "";
    private string _firmwareVersion = "";
    private string _dllVersion = "";
    private string _apiVersion = "";

    public bool IsConnected => _libraryHandle != IntPtr.Zero && _deviceId != 0 && _channelId != 0;
    public string ConnectionName { get; private set; } = "Disconnected";

    public void Connect(string dllPath)
    {
        Disconnect();

        if (string.IsNullOrWhiteSpace(dllPath))
        {
            throw new ArgumentException("Select a J2534 DLL before connecting.", nameof(dllPath));
        }

        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException("The selected J2534 DLL does not exist.", dllPath);
        }

        try
        {
            _libraryHandle = NativeLibrary.Load(dllPath);
            LoadDelegates();

            ThrowIfError(_open!(IntPtr.Zero, out _deviceId), "PassThruOpen");
            ReadVersions();
            ThrowIfError(_connect!(_deviceId, ProtocolIso15765, flags: 0, BaudCan500K, out _channelId), "PassThruConnect");

            _dllPath = dllPath;
            _currentRequestId = FunctionalRequestId;
            _headersOn = false;
            _spacesOn = true;
            ConnectionName = $"J2534: {Path.GetFileName(dllPath)}";

            StartToyotaCanFlowControlFilters();
        }
        catch (BadImageFormatException ex)
        {
            Disconnect();
            throw new InvalidOperationException("The J2534 DLL could not be loaded because its 32-bit/64-bit architecture does not match this app. Install a matching driver or build TacomaDiag for the DLL architecture.", ex);
        }
        catch
        {
            Disconnect();
            throw;
        }
    }

    public async Task<string> SendCommandAsync(string command, int timeoutMs = 4000, CancellationToken cancellationToken = default)
    {
        command = NormalizeCommand(command);
        if (string.IsNullOrWhiteSpace(command))
        {
            return "";
        }

        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            if (TryHandleAtCommand(command, out var atResponse))
            {
                return atResponse;
            }

            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to a J2534 adapter.");
            }

            var requestPayload = ParseHexPayload(command);
            if (requestPayload.Length == 0)
            {
                return "";
            }

            return await Task.Run(() =>
            {
                return SendObdCommandBlocking(requestPayload, timeoutMs, cancellationToken);
            }, cancellationToken);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public void Disconnect()
    {
        foreach (var filterId in _filterIds.ToArray())
        {
            try
            {
                if (_channelId != 0)
                {
                    _stopMsgFilter?.Invoke(_channelId, filterId);
                }
            }
            catch
            {
            }
        }

        _filterIds.Clear();

        try
        {
            if (_channelId != 0)
            {
                _disconnect?.Invoke(_channelId);
            }
        }
        catch
        {
        }

        _channelId = 0;

        try
        {
            if (_deviceId != 0)
            {
                _close?.Invoke(_deviceId);
            }
        }
        catch
        {
        }

        _deviceId = 0;

        if (_libraryHandle != IntPtr.Zero)
        {
            NativeLibrary.Free(_libraryHandle);
            _libraryHandle = IntPtr.Zero;
        }

        _open = null;
        _close = null;
        _connect = null;
        _disconnect = null;
        _readMsgs = null;
        _writeMsgs = null;
        _startMsgFilter = null;
        _stopMsgFilter = null;
        _readVersion = null;
        _getLastError = null;
        _dllPath = "";
        _firmwareVersion = "";
        _dllVersion = "";
        _apiVersion = "";
        ConnectionName = "Disconnected";
    }

    public void Dispose()
    {
        Disconnect();
        _ioLock.Dispose();
    }

    private void LoadDelegates()
    {
        _open = LoadRequiredDelegate<PassThruOpenDelegate>("PassThruOpen");
        _close = LoadRequiredDelegate<PassThruCloseDelegate>("PassThruClose");
        _connect = LoadRequiredDelegate<PassThruConnectDelegate>("PassThruConnect");
        _disconnect = LoadRequiredDelegate<PassThruDisconnectDelegate>("PassThruDisconnect");
        _readMsgs = LoadRequiredDelegate<PassThruReadMsgsDelegate>("PassThruReadMsgs");
        _writeMsgs = LoadRequiredDelegate<PassThruWriteMsgsDelegate>("PassThruWriteMsgs");
        _startMsgFilter = LoadRequiredDelegate<PassThruStartMsgFilterDelegate>("PassThruStartMsgFilter");
        _stopMsgFilter = LoadRequiredDelegate<PassThruStopMsgFilterDelegate>("PassThruStopMsgFilter");
        _readVersion = LoadRequiredDelegate<PassThruReadVersionDelegate>("PassThruReadVersion");
        _getLastError = LoadRequiredDelegate<PassThruGetLastErrorDelegate>("PassThruGetLastError");
    }

    private T LoadRequiredDelegate<T>(string exportName)
        where T : Delegate
    {
        if (!NativeLibrary.TryGetExport(_libraryHandle, exportName, out var address))
        {
            throw new MissingMethodException($"The selected J2534 DLL does not export {exportName}.");
        }

        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    private void ReadVersions()
    {
        var firmware = new StringBuilder(80);
        var dll = new StringBuilder(80);
        var api = new StringBuilder(80);
        if (_readVersion?.Invoke(_deviceId, firmware, dll, api) == StatusNoError)
        {
            _firmwareVersion = firmware.ToString();
            _dllVersion = dll.ToString();
            _apiVersion = api.ToString();
        }
    }

    private void StartToyotaCanFlowControlFilters()
    {
        if (_startMsgFilter is null || _channelId == 0)
        {
            return;
        }

        unsafe
        {
            for (uint module = 0; module <= 7; module++)
            {
                var mask = CreateCanIdOnlyMessage(0xFFFFFFFF);
                var pattern = CreateCanIdOnlyMessage(0x000007E8 + module);
                var flowControl = CreateCanIdOnlyMessage(0x000007E0 + module);

                uint filterId = 0;
                var status = _startMsgFilter(_channelId, FlowControlFilter, &mask, &pattern, &flowControl, out filterId);
                if (status == StatusNoError)
                {
                    _filterIds.Add(filterId);
                }
            }
        }
    }

    private string SendObdCommandBlocking(byte[] requestPayload, int timeoutMs, CancellationToken cancellationToken)
    {
        DrainReadBuffer(cancellationToken);
        var requestId = ResolveRequestCanId(requestPayload);
        WriteIso15765Message(requestId, requestPayload, timeoutMs);
        var responses = ReadIso15765Responses(requestPayload, timeoutMs, cancellationToken);
        return responses.Count == 0 ? "NO DATA" : string.Join(Environment.NewLine, responses);
    }

    private void DrainReadBuffer(CancellationToken cancellationToken)
    {
        if (_readMsgs is null || _channelId == 0)
        {
            return;
        }

        unsafe
        {
            var messages = stackalloc PassThruMsg[MaxReadMessages];
            for (var i = 0; i < 10; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                uint messageCount = MaxReadMessages;
                var status = _readMsgs(_channelId, messages, ref messageCount, timeout: 0);
                if (status != StatusNoError || messageCount == 0)
                {
                    break;
                }
            }
        }
    }

    private void WriteIso15765Message(uint requestId, byte[] payload, int timeoutMs)
    {
        if (_writeMsgs is null || _channelId == 0)
        {
            throw new InvalidOperationException("J2534 write function is not available.");
        }

        unsafe
        {
            var message = CreateIso15765Message(requestId, payload);
            uint count = 1;
            var status = _writeMsgs(_channelId, &message, ref count, (uint)Math.Max(timeoutMs, 1000));
            ThrowIfError(status, "PassThruWriteMsgs");
        }
    }

    private IReadOnlyList<string> ReadIso15765Responses(byte[] requestPayload, int timeoutMs, CancellationToken cancellationToken)
    {
        if (_readMsgs is null || _channelId == 0)
        {
            throw new InvalidOperationException("J2534 read function is not available.");
        }

        var responses = new List<string>();
        var expectedPositiveService = requestPayload[0] + 0x40;
        var stopwatch = Stopwatch.StartNew();

        unsafe
        {
            var messages = stackalloc PassThruMsg[MaxReadMessages];
            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                uint messageCount = MaxReadMessages;
                var status = _readMsgs(_channelId, messages, ref messageCount, timeout: 75);

                if (status != StatusNoError || messageCount == 0)
                {
                    Thread.Sleep(10);
                    continue;
                }

                for (var i = 0; i < messageCount; i++)
                {
                    var canId = ReadCanId(&messages[i]);
                    var payload = ReadPayload(&messages[i]);
                    if (payload.Length == 0)
                    {
                        continue;
                    }

                    if (payload[0] == expectedPositiveService || payload[0] == 0x7F)
                    {
                        responses.Add(FormatPayload(canId, payload));
                    }
                }

                if (responses.Count > 0)
                {
                    break;
                }
            }
        }

        return responses;
    }

    private bool TryHandleAtCommand(string command, out string response)
    {
        response = "";
        if (!command.StartsWith("AT", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (command is "ATZ" or "ATWS")
        {
            _currentRequestId = FunctionalRequestId;
            _headersOn = false;
            _spacesOn = true;
            response = "J2534 PASS-THRU";
            return true;
        }

        if (command is "ATI")
        {
            response = string.IsNullOrWhiteSpace(_dllVersion)
                ? $"J2534 {Path.GetFileName(_dllPath)}"
                : $"J2534 DLL {_dllVersion}; API {_apiVersion}; FW {_firmwareVersion}";
            return true;
        }

        if (command is "ATDP")
        {
            response = $"J2534 ISO 15765-4 (CAN 11/500) via {Path.GetFileName(_dllPath)}";
            return true;
        }

        if (command is "ATDPN")
        {
            response = "A6";
            return true;
        }

        if (command is "ATH1")
        {
            _headersOn = true;
            response = "OK";
            return true;
        }

        if (command is "ATH0")
        {
            _headersOn = false;
            response = "OK";
            return true;
        }

        if (command is "ATS1")
        {
            _spacesOn = true;
            response = "OK";
            return true;
        }

        if (command is "ATS0")
        {
            _spacesOn = false;
            response = "OK";
            return true;
        }

        if (command.StartsWith("ATSH", StringComparison.OrdinalIgnoreCase))
        {
            var header = command[4..];
            if (!uint.TryParse(header, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var canId))
            {
                response = "?";
                return true;
            }

            _currentRequestId = canId;
            response = "OK";
            return true;
        }

        if (command.StartsWith("ATSP", StringComparison.OrdinalIgnoreCase) ||
            command.StartsWith("ATAT", StringComparison.OrdinalIgnoreCase) ||
            command.StartsWith("ATST", StringComparison.OrdinalIgnoreCase) ||
            command.StartsWith("ATE", StringComparison.OrdinalIgnoreCase) ||
            command.StartsWith("ATL", StringComparison.OrdinalIgnoreCase))
        {
            response = "OK";
            return true;
        }

        response = "?";
        return true;
    }

    private uint ResolveRequestCanId(byte[] payload)
    {
        if (_currentRequestId != FunctionalRequestId)
        {
            return _currentRequestId;
        }

        return payload[0] is 0x06 or 0x09 ? EngineRequestId : FunctionalRequestId;
    }

    private string FormatPayload(uint canId, byte[] payload)
    {
        var data = _spacesOn
            ? string.Join(" ", payload.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)))
            : string.Concat(payload.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));

        return _headersOn ? $"{canId:X3} {data}" : data;
    }

    private string GetLastError()
    {
        if (_getLastError is null)
        {
            return "";
        }

        var error = new StringBuilder(256);
        return _getLastError(error) == StatusNoError ? error.ToString() : "";
    }

    private void ThrowIfError(int status, string operation)
    {
        if (status == StatusNoError)
        {
            return;
        }

        var detail = GetLastError();
        var suffix = string.IsNullOrWhiteSpace(detail) ? "" : $" - {detail}";
        throw new InvalidOperationException($"{operation} failed with J2534 status 0x{status:X8}{suffix}");
    }

    private static PassThruMsg CreateCanIdOnlyMessage(uint canId)
    {
        var message = new PassThruMsg
        {
            ProtocolID = ProtocolIso15765,
            DataSize = 4,
            ExtraDataIndex = 0
        };

        unsafe
        {
            WriteCanId(&message, canId);
        }

        return message;
    }

    private static PassThruMsg CreateIso15765Message(uint canId, ReadOnlySpan<byte> payload)
    {
        if (payload.Length + 4 > MaxDataSize)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), "The ISO 15765 payload is too large for a J2534 message.");
        }

        var message = new PassThruMsg
        {
            ProtocolID = ProtocolIso15765,
            TxFlags = Iso15765FramePad,
            DataSize = (uint)(payload.Length + 4),
            ExtraDataIndex = 0
        };

        unsafe
        {
            WriteCanId(&message, canId);
            var data = message.Data;
            for (var i = 0; i < payload.Length; i++)
            {
                data[i + 4] = payload[i];
            }
        }

        return message;
    }

    private static unsafe void WriteCanId(PassThruMsg* message, uint canId)
    {
        var data = message->Data;
        data[0] = (byte)((canId >> 24) & 0xFF);
        data[1] = (byte)((canId >> 16) & 0xFF);
        data[2] = (byte)((canId >> 8) & 0xFF);
        data[3] = (byte)(canId & 0xFF);
    }

    private static unsafe uint ReadCanId(PassThruMsg* message)
    {
        if (message->DataSize < 4)
        {
            return 0;
        }

        var data = message->Data;
        return ((uint)data[0] << 24) | ((uint)data[1] << 16) | ((uint)data[2] << 8) | data[3];
    }

    private static unsafe byte[] ReadPayload(PassThruMsg* message)
    {
        if (message->DataSize <= 4)
        {
            return [];
        }

        var length = (int)message->DataSize - 4;
        var payload = new byte[length];
        var data = message->Data;
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = data[i + 4];
        }

        return payload;
    }

    private static byte[] ParseHexPayload(string command)
    {
        if (command.Length % 2 != 0)
        {
            throw new FormatException("OBD command hex must contain an even number of characters.");
        }

        var payload = new byte[command.Length / 2];
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = Convert.ToByte(command.Substring(i * 2, 2), 16);
        }

        return payload;
    }

    private static string NormalizeCommand(string command)
    {
        return command.Trim().Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct PassThruMsg
    {
        public uint ProtocolID;
        public uint RxStatus;
        public uint TxFlags;
        public uint Timestamp;
        public uint DataSize;
        public uint ExtraDataIndex;
        public fixed byte Data[MaxDataSize];
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int PassThruOpenDelegate(IntPtr pName, out uint pDeviceId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int PassThruCloseDelegate(uint deviceId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int PassThruConnectDelegate(uint deviceId, uint protocolId, uint flags, uint baudRate, out uint channelId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int PassThruDisconnectDelegate(uint channelId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private unsafe delegate int PassThruReadMsgsDelegate(uint channelId, PassThruMsg* pMsg, ref uint pNumMsgs, uint timeout);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private unsafe delegate int PassThruWriteMsgsDelegate(uint channelId, PassThruMsg* pMsg, ref uint pNumMsgs, uint timeout);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private unsafe delegate int PassThruStartMsgFilterDelegate(uint channelId, uint filterType, PassThruMsg* pMaskMsg, PassThruMsg* pPatternMsg, PassThruMsg* pFlowControlMsg, out uint filterId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int PassThruStopMsgFilterDelegate(uint channelId, uint filterId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate int PassThruReadVersionDelegate(uint deviceId, StringBuilder firmwareVersion, StringBuilder dllVersion, StringBuilder apiVersion);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate int PassThruGetLastErrorDelegate(StringBuilder errorDescription);
}
