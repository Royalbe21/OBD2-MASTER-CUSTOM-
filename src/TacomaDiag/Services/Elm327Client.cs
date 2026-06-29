using System.Diagnostics;
using System.IO.Ports;
using System.Text;

namespace TacomaDiag.Services;

public sealed class Elm327Client : IObdTransport
{
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private SerialPort? _serialPort;

    public bool IsDemoMode { get; private set; }
    public bool IsConnected => IsDemoMode || _serialPort?.IsOpen == true;
    public string ConnectionName { get; private set; } = "Disconnected";

    public static string[] GetSerialPorts()
    {
        return SerialPort.GetPortNames().OrderBy(port => port).ToArray();
    }

    public void ConnectDemo()
    {
        Disconnect();
        IsDemoMode = true;
        ConnectionName = "Demo ELM327";
    }

    public void ConnectSerial(string portName, int baudRate)
    {
        Disconnect();

        _serialPort = new SerialPort(portName, baudRate)
        {
            NewLine = "\r",
            ReadTimeout = 250,
            WriteTimeout = 1500,
            DtrEnable = true,
            RtsEnable = true
        };

        _serialPort.Open();
        _serialPort.DiscardInBuffer();
        _serialPort.DiscardOutBuffer();

        IsDemoMode = false;
        ConnectionName = $"{portName} @ {baudRate}";
    }

    public void Disconnect()
    {
        IsDemoMode = false;
        ConnectionName = "Disconnected";

        if (_serialPort is null)
        {
            return;
        }

        try
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }
        finally
        {
            _serialPort.Dispose();
            _serialPort = null;
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
            if (IsDemoMode)
            {
                await Task.Delay(80, cancellationToken);
                return DemoElm327Responder.GetResponse(command);
            }

            if (_serialPort?.IsOpen != true)
            {
                throw new InvalidOperationException("Not connected to an ELM327 adapter.");
            }

            return await Task.Run(() => SendCommandBlocking(command, timeoutMs, cancellationToken), cancellationToken);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private string SendCommandBlocking(string command, int timeoutMs, CancellationToken cancellationToken)
    {
        if (_serialPort is null)
        {
            throw new InvalidOperationException("Serial port is not open.");
        }

        _serialPort.DiscardInBuffer();
        _serialPort.Write(command + "\r");

        var response = new StringBuilder();
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var next = _serialPort.ReadChar();
                if (next < 0)
                {
                    continue;
                }

                var ch = (char)next;
                if (ch == '>')
                {
                    break;
                }

                response.Append(ch);
            }
            catch (TimeoutException)
            {
                Thread.Sleep(15);
            }
        }

        return CleanResponse(command, response.ToString());
    }

    private static string CleanResponse(string command, string response)
    {
        var lines = response
            .Replace('\0', ' ')
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Where(line => !line.Equals(command, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return string.Join(Environment.NewLine, lines);
    }

    private static string NormalizeCommand(string command)
    {
        return command.Trim().Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
    }

    public void Dispose()
    {
        Disconnect();
        _ioLock.Dispose();
    }

    private static class DemoElm327Responder
    {
        private static readonly Dictionary<string, string> Responses = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ATZ"] = "ELM327 v1.5",
            ["ATI"] = "ELM327 v1.5",
            ["ATE0"] = "OK",
            ["ATL0"] = "OK",
            ["ATS0"] = "OK",
            ["ATS1"] = "OK",
            ["ATH0"] = "OK",
            ["ATH1"] = "OK",
            ["ATSP0"] = "OK",
            ["ATSP6"] = "OK",
            ["ATAT1"] = "OK",
            ["ATST96"] = "OK",
            ["ATDPN"] = "A6",
            ["ATDP"] = "ISO 15765-4 (CAN 11/500)",
            ["ATSH7DF"] = "OK",
            ["ATSH7E0"] = "OK",
            ["ATSH7E1"] = "OK",
            ["ATSH7E2"] = "OK",
            ["ATSH7E3"] = "OK",
            ["ATSH7E4"] = "OK",
            ["ATSH7E5"] = "OK",
            ["ATSH7E6"] = "OK",
            ["ATSH7E7"] = "OK",
            ["0100"] = "41 00 BE 3F A8 13",
            ["0120"] = "41 20 80 01 A0 01",
            ["0140"] = "41 40 40 08 00 00",
            ["0101"] = "41 01 00 07 ED 00",
            ["0104"] = "41 04 28",
            ["0105"] = "41 05 58",
            ["0106"] = "41 06 84",
            ["0107"] = "41 07 82",
            ["010B"] = "41 0B 24",
            ["010C"] = "41 0C 0B B8",
            ["010D"] = "41 0D 00",
            ["010E"] = "41 0E 80",
            ["010F"] = "41 0F 42",
            ["0110"] = "41 10 01 2C",
            ["0111"] = "41 11 2A",
            ["011F"] = "41 1F 00 7B",
            ["012F"] = "41 2F A8",
            ["0133"] = "41 33 63",
            ["0142"] = "41 42 34 C0",
            ["0145"] = "41 45 21",
            ["0146"] = "41 46 44",
            ["03"] = "43 00 00",
            ["07"] = "47 00 00",
            ["0A"] = "4A 00 00",
            ["04"] = "44",
            ["0902"] = "49 02 01 00 35 54 45 4E 58 32\r49 02 02 32 4E 30 38 5A 30 30\r49 02 03 30 30 30 31 00 00 00",
            ["0600"] = "46 00 C0 00 00 01",
            ["0620"] = "46 20 00 00 00 00",
            ["0640"] = "NO DATA",
            ["0660"] = "NO DATA",
            ["0680"] = "NO DATA",
            ["06A0"] = "NO DATA",
            ["06C0"] = "NO DATA",
            ["06E0"] = "NO DATA"
        };

        public static string GetResponse(string command)
        {
            if (Responses.TryGetValue(command, out var response))
            {
                return response;
            }

            if (command.StartsWith("ATSH7E", StringComparison.OrdinalIgnoreCase))
            {
                return "OK";
            }

            return command.StartsWith("01", StringComparison.OrdinalIgnoreCase) ? "NO DATA" : "?";
        }
    }
}
