using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TacomaDiag.Models;

namespace TacomaDiag.Services;

public static partial class ObdDecoder
{
    public static IReadOnlyList<LivePidDefinition> LivePidDefinitions { get; } =
    [
        new("0104", "Calculated engine load", "%"),
        new("0105", "Engine coolant temperature", "C"),
        new("0106", "Short term fuel trim bank 1", "%"),
        new("0107", "Long term fuel trim bank 1", "%"),
        new("010B", "Intake manifold pressure", "kPa"),
        new("010C", "Engine RPM", "rpm"),
        new("010D", "Vehicle speed", "km/h"),
        new("010E", "Timing advance", "deg"),
        new("010F", "Intake air temperature", "C"),
        new("0110", "Mass air flow", "g/s"),
        new("0111", "Throttle position", "%"),
        new("011F", "Run time since engine start", "s"),
        new("012F", "Fuel level input", "%"),
        new("0133", "Barometric pressure", "kPa"),
        new("0142", "Control module voltage", "V"),
        new("0145", "Relative throttle position", "%"),
        new("0146", "Ambient air temperature", "C")
    ];

    private static readonly Dictionary<string, string> DtcDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["P0100"] = "Mass or volume air flow circuit",
        ["P0101"] = "Mass air flow circuit range/performance",
        ["P0110"] = "Intake air temperature sensor circuit",
        ["P0115"] = "Engine coolant temperature circuit",
        ["P0120"] = "Throttle/pedal position sensor circuit",
        ["P0130"] = "Oxygen sensor circuit bank 1 sensor 1",
        ["P0135"] = "Oxygen sensor heater circuit bank 1 sensor 1",
        ["P0171"] = "System too lean bank 1",
        ["P0172"] = "System too rich bank 1",
        ["P0300"] = "Random/multiple cylinder misfire",
        ["P0301"] = "Cylinder 1 misfire",
        ["P0302"] = "Cylinder 2 misfire",
        ["P0303"] = "Cylinder 3 misfire",
        ["P0304"] = "Cylinder 4 misfire",
        ["P0420"] = "Catalyst system efficiency below threshold bank 1",
        ["P0440"] = "Evaporative emission control system",
        ["P0441"] = "Evaporative emission system incorrect purge flow",
        ["P0442"] = "Evaporative emission system small leak",
        ["P0446"] = "Evaporative emission vent control circuit",
        ["P0455"] = "Evaporative emission system gross leak",
        ["P0500"] = "Vehicle speed sensor",
        ["P0606"] = "PCM/ECM processor fault"
    };

    public static IReadOnlyList<DiagnosticTroubleCode> DecodeDtcResponse(string rawResponse, string type)
    {
        var positiveResponse = type.Equals("Pending", StringComparison.OrdinalIgnoreCase) ? 0x47 :
            type.Equals("Permanent", StringComparison.OrdinalIgnoreCase) ? 0x4A : 0x43;

        var payloads = FindPayloads(rawResponse, positiveResponse).ToArray();
        if (payloads.Length == 0)
        {
            return [];
        }

        var results = new List<DiagnosticTroubleCode>();
        foreach (var payload in payloads)
        {
            for (var i = 1; i + 1 < payload.Count; i += 2)
            {
                if (payload[i] == 0x00 && payload[i + 1] == 0x00)
                {
                    continue;
                }

                var code = DecodeDtc(payload[i], payload[i + 1]);
                results.Add(new DiagnosticTroubleCode
                {
                    Type = type,
                    Code = code,
                    Description = DtcDescriptions.TryGetValue(code, out var description) ? description : "Description not in local database",
                    RawResponse = rawResponse
                });
            }
        }

        return results;
    }

    public static ReadinessSnapshot DecodeReadiness(string rawResponse)
    {
        var payload = FindPayloads(rawResponse, 0x41, 0x01).FirstOrDefault();
        if (payload is null || payload.Count < 6)
        {
            throw new FormatException("The response did not contain a complete 41 01 readiness payload.");
        }

        var a = payload[2];
        var b = payload[3];
        var c = payload[4];
        var d = payload[5];
        var compressionIgnition = IsBitSet(b, 3);

        var monitors = new List<MonitorStatus>
        {
            BuildMonitor("Misfire", b, 0, b, 4),
            BuildMonitor("Fuel system", b, 1, b, 5),
            BuildMonitor("Comprehensive components", b, 2, b, 6)
        };

        if (compressionIgnition)
        {
            monitors.AddRange(
            [
                BuildMonitor("NMHC catalyst", c, 0, d, 0),
                BuildMonitor("NOx/SCR monitor", c, 1, d, 1),
                BuildMonitor("Boost pressure", c, 3, d, 3),
                BuildMonitor("Exhaust gas sensor", c, 5, d, 5),
                BuildMonitor("PM filter", c, 6, d, 6),
                BuildMonitor("EGR/VVT system", c, 7, d, 7)
            ]);
        }
        else
        {
            monitors.AddRange(
            [
                BuildMonitor("Catalyst", c, 0, d, 0),
                BuildMonitor("Heated catalyst", c, 1, d, 1),
                BuildMonitor("EVAP system", c, 2, d, 2),
                BuildMonitor("Secondary air system", c, 3, d, 3),
                BuildMonitor("Gasoline particulate filter", c, 4, d, 4),
                BuildMonitor("Oxygen sensor", c, 5, d, 5),
                BuildMonitor("Oxygen sensor heater", c, 6, d, 6),
                BuildMonitor("EGR/VVT system", c, 7, d, 7)
            ]);
        }

        return new ReadinessSnapshot
        {
            MilOn = IsBitSet(a, 7),
            ConfirmedDtcCount = a & 0x7F,
            EngineType = compressionIgnition ? "Compression ignition" : "Spark ignition",
            RawResponse = rawResponse,
            Monitors = monitors
        };
    }

    public static IReadOnlySet<int> DecodeSupportedPids(string rawResponse, int basePid)
    {
        var payload = FindPayloads(rawResponse, 0x41, basePid).FirstOrDefault();
        if (payload is null || payload.Count < 6)
        {
            return new HashSet<int>();
        }

        var supported = new HashSet<int>();
        var bytes = payload.Skip(2).Take(4).ToArray();

        for (var byteIndex = 0; byteIndex < bytes.Length; byteIndex++)
        {
            for (var bit = 7; bit >= 0; bit--)
            {
                if ((bytes[byteIndex] & (1 << bit)) == 0)
                {
                    continue;
                }

                var offset = byteIndex * 8 + (7 - bit) + 1;
                supported.Add(basePid + offset);
            }
        }

        return supported;
    }

    public static LivePidReading DecodeLivePid(LivePidDefinition definition, string rawResponse, bool supported)
    {
        var pid = Convert.ToInt32(definition.Pid[2..], 16);
        var payload = FindPayloads(rawResponse, 0x41, pid).FirstOrDefault();

        if (payload is null)
        {
            return new LivePidReading
            {
                Pid = definition.Pid,
                Name = definition.Name,
                Unit = definition.Unit,
                Supported = supported ? "Yes" : "Unknown",
                Value = "No data",
                RawResponse = rawResponse
            };
        }

        var data = payload.Skip(2).ToArray();
        var value = TryConvertLiveValue(definition.Pid, data, out var converted)
            ? converted
            : "Raw " + string.Join(" ", data.Select(valueByte => valueByte.ToString("X2", CultureInfo.InvariantCulture)));

        return new LivePidReading
        {
            Pid = definition.Pid,
            Name = definition.Name,
            Value = value,
            Unit = definition.Unit,
            Supported = supported ? "Yes" : "Unknown",
            RawResponse = rawResponse
        };
    }

    public static string DecodeVin(string rawResponse)
    {
        var payloads = FindPayloads(rawResponse, 0x49, 0x02).ToArray();
        if (payloads.Length == 0)
        {
            return "";
        }

        var bytes = new List<byte>();
        foreach (var payload in payloads)
        {
            var start = 2;
            if (payload.Count > 2 && payload[2] is >= 0x01 and <= 0x09)
            {
                start = 3;
            }

            bytes.AddRange(payload.Skip(start));
        }

        var vin = new string(bytes
            .Where(value => value is >= 0x20 and <= 0x7E)
            .Select(value => (char)value)
            .ToArray());

        return vin.Trim('\0', ' ', '\r', '\n');
    }

    public static string BuildQuickReport(
        string connection,
        string protocol,
        string vin,
        IEnumerable<DiagnosticTroubleCode> dtcs,
        ReadinessSnapshot? readiness)
    {
        var builder = new StringBuilder();
        builder.AppendLine("TacomaDiag Report");
        builder.AppendLine($"Created: {DateTime.Now:G}");
        builder.AppendLine($"Connection: {connection}");
        builder.AppendLine($"Protocol: {protocol}");
        builder.AppendLine($"VIN: {(string.IsNullOrWhiteSpace(vin) ? "Not read" : vin)}");
        builder.AppendLine();

        builder.AppendLine("DTCs");
        var dtcList = dtcs.ToList();
        if (dtcList.Count == 0)
        {
            builder.AppendLine("No DTCs currently listed in the app.");
        }
        else
        {
            foreach (var dtc in dtcList)
            {
                builder.AppendLine($"{dtc.Type}: {dtc.Code} - {dtc.Description}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Readiness");
        if (readiness is null)
        {
            builder.AppendLine("Readiness has not been checked.");
        }
        else
        {
            builder.AppendLine($"MIL: {(readiness.MilOn ? "On" : "Off")}");
            builder.AppendLine($"Confirmed emissions DTC count: {readiness.ConfirmedDtcCount}");
            builder.AppendLine($"Engine type: {readiness.EngineType}");
            foreach (var monitor in readiness.Monitors)
            {
                builder.AppendLine($"{monitor.Name}: {monitor.Status}");
            }
        }

        return builder.ToString();
    }

    private static bool TryConvertLiveValue(string pid, byte[] data, out string value)
    {
        value = "";
        if (data.Length == 0)
        {
            return false;
        }

        double converted;
        switch (pid)
        {
            case "0104":
            case "0111":
            case "012F":
            case "0145":
                converted = data[0] * 100.0 / 255.0;
                value = converted.ToString("0.0", CultureInfo.InvariantCulture);
                return true;
            case "0105":
            case "010F":
            case "0146":
                value = (data[0] - 40).ToString(CultureInfo.InvariantCulture);
                return true;
            case "0106":
            case "0107":
                converted = data[0] / 1.28 - 100.0;
                value = converted.ToString("0.0", CultureInfo.InvariantCulture);
                return true;
            case "010B":
            case "0133":
                value = data[0].ToString(CultureInfo.InvariantCulture);
                return true;
            case "010C" when data.Length >= 2:
                converted = ((data[0] * 256.0) + data[1]) / 4.0;
                value = converted.ToString("0", CultureInfo.InvariantCulture);
                return true;
            case "010D":
                value = data[0].ToString(CultureInfo.InvariantCulture);
                return true;
            case "010E":
                converted = data[0] / 2.0 - 64.0;
                value = converted.ToString("0.0", CultureInfo.InvariantCulture);
                return true;
            case "0110" when data.Length >= 2:
                converted = ((data[0] * 256.0) + data[1]) / 100.0;
                value = converted.ToString("0.00", CultureInfo.InvariantCulture);
                return true;
            case "011F" when data.Length >= 2:
                value = ((data[0] * 256) + data[1]).ToString(CultureInfo.InvariantCulture);
                return true;
            case "0142" when data.Length >= 2:
                converted = ((data[0] * 256.0) + data[1]) / 1000.0;
                value = converted.ToString("0.00", CultureInfo.InvariantCulture);
                return true;
            default:
                return false;
        }
    }

    private static MonitorStatus BuildMonitor(string name, int supportByte, int supportBit, int incompleteByte, int incompleteBit)
    {
        var supported = IsBitSet(supportByte, supportBit);
        if (!supported)
        {
            return new MonitorStatus
            {
                Name = name,
                Supported = "No",
                Ready = "",
                Status = "Not supported"
            };
        }

        var incomplete = IsBitSet(incompleteByte, incompleteBit);
        return new MonitorStatus
        {
            Name = name,
            Supported = "Yes",
            Ready = incomplete ? "No" : "Yes",
            Status = incomplete ? "Not ready" : "Ready"
        };
    }

    private static string DecodeDtc(byte first, byte second)
    {
        var system = ((first & 0xC0) >> 6) switch
        {
            0 => "P",
            1 => "C",
            2 => "B",
            3 => "U",
            _ => "P"
        };

        return string.Create(CultureInfo.InvariantCulture, $"{system}{(first & 0x30) >> 4:X1}{first & 0x0F:X1}{(second & 0xF0) >> 4:X1}{second & 0x0F:X1}");
    }

    private static IEnumerable<IReadOnlyList<byte>> FindPayloads(string rawResponse, int positiveResponse, int? pid = null)
    {
        foreach (var line in SplitResponseLines(rawResponse))
        {
            var bytes = ExtractBytes(line);
            for (var i = 0; i < bytes.Count; i++)
            {
                if (bytes[i] != positiveResponse)
                {
                    continue;
                }

                if (pid.HasValue && (i + 1 >= bytes.Count || bytes[i + 1] != pid.Value))
                {
                    continue;
                }

                yield return bytes.Skip(i).ToArray();
                break;
            }
        }
    }

    private static IReadOnlyList<string> SplitResponseLines(string rawResponse)
    {
        return rawResponse
            .Replace(">", "", StringComparison.Ordinal)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().ToUpperInvariant())
            .Where(line => line.Length > 0)
            .Where(line => !line.StartsWith("SEARCHING", StringComparison.Ordinal))
            .Where(line => !line.Contains("NO DATA", StringComparison.Ordinal))
            .Where(line => !line.Contains("UNABLE", StringComparison.Ordinal))
            .Where(line => !line.Contains("STOPPED", StringComparison.Ordinal))
            .Where(line => !line.Equals("OK", StringComparison.Ordinal))
            .Where(line => !line.Equals("?", StringComparison.Ordinal))
            .ToArray();
    }

    private static IReadOnlyList<byte> ExtractBytes(string line)
    {
        var tokenBytes = line.Split(new[] { ' ', '\t', ':', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length == 2 && HexByteRegex().IsMatch(token))
            .Select(token => Convert.ToByte(token, 16))
            .ToList();

        if (tokenBytes.Count > 0)
        {
            return tokenBytes;
        }

        var compact = NonHexRegex().Replace(line, "");
        if (compact.Length % 2 != 0)
        {
            compact = compact[1..];
        }

        var bytes = new List<byte>();
        for (var i = 0; i + 1 < compact.Length; i += 2)
        {
            bytes.Add(Convert.ToByte(compact.Substring(i, 2), 16));
        }

        return bytes;
    }

    private static bool IsBitSet(int value, int bit)
    {
        return (value & (1 << bit)) != 0;
    }

    [GeneratedRegex("^[0-9A-F]{2}$", RegexOptions.IgnoreCase)]
    private static partial Regex HexByteRegex();

    [GeneratedRegex("[^0-9A-F]", RegexOptions.IgnoreCase)]
    private static partial Regex NonHexRegex();
}
