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
        ["P0000"] = "No diagnostic trouble codes reported",
        ["P0010"] = "Camshaft position actuator circuit bank 1",
        ["P0011"] = "Camshaft position timing over-advanced or system performance bank 1",
        ["P0012"] = "Camshaft position timing over-retarded bank 1",
        ["P0013"] = "Camshaft position actuator circuit bank 1 exhaust",
        ["P0014"] = "Camshaft position timing over-advanced bank 1 exhaust",
        ["P0015"] = "Camshaft position timing over-retarded bank 1 exhaust",
        ["P0016"] = "Crankshaft/camshaft position correlation bank 1 sensor A",
        ["P0017"] = "Crankshaft/camshaft position correlation bank 1 sensor B",
        ["P0031"] = "Oxygen/A-F sensor heater control circuit low bank 1 sensor 1",
        ["P0032"] = "Oxygen/A-F sensor heater control circuit high bank 1 sensor 1",
        ["P0037"] = "Oxygen sensor heater control circuit low bank 1 sensor 2",
        ["P0038"] = "Oxygen sensor heater control circuit high bank 1 sensor 2",
        ["P0051"] = "Oxygen/A-F sensor heater control circuit low bank 2 sensor 1",
        ["P0052"] = "Oxygen/A-F sensor heater control circuit high bank 2 sensor 1",
        ["P0057"] = "Oxygen sensor heater control circuit low bank 2 sensor 2",
        ["P0058"] = "Oxygen sensor heater control circuit high bank 2 sensor 2",
        ["P0100"] = "Mass or volume air flow circuit",
        ["P0101"] = "Mass air flow circuit range/performance",
        ["P0102"] = "Mass air flow circuit low input",
        ["P0103"] = "Mass air flow circuit high input",
        ["P0105"] = "Manifold absolute pressure/barometric pressure circuit",
        ["P0106"] = "Manifold absolute pressure/barometric pressure range/performance",
        ["P0107"] = "Manifold absolute pressure/barometric pressure low input",
        ["P0108"] = "Manifold absolute pressure/barometric pressure high input",
        ["P0110"] = "Intake air temperature sensor circuit",
        ["P0112"] = "Intake air temperature sensor circuit low input",
        ["P0113"] = "Intake air temperature sensor circuit high input",
        ["P0115"] = "Engine coolant temperature circuit",
        ["P0116"] = "Engine coolant temperature circuit range/performance",
        ["P0117"] = "Engine coolant temperature circuit low input",
        ["P0118"] = "Engine coolant temperature circuit high input",
        ["P0120"] = "Throttle/pedal position sensor circuit",
        ["P0121"] = "Throttle/pedal position sensor circuit range/performance",
        ["P0122"] = "Throttle/pedal position sensor circuit low input",
        ["P0123"] = "Throttle/pedal position sensor circuit high input",
        ["P0125"] = "Insufficient coolant temperature for closed-loop fuel control",
        ["P0128"] = "Coolant thermostat below regulating temperature",
        ["P0130"] = "Oxygen sensor circuit bank 1 sensor 1",
        ["P0131"] = "Oxygen sensor circuit low voltage bank 1 sensor 1",
        ["P0132"] = "Oxygen sensor circuit high voltage bank 1 sensor 1",
        ["P0133"] = "Oxygen sensor circuit slow response bank 1 sensor 1",
        ["P0134"] = "Oxygen sensor circuit no activity detected bank 1 sensor 1",
        ["P0135"] = "Oxygen sensor heater circuit bank 1 sensor 1",
        ["P0136"] = "Oxygen sensor circuit bank 1 sensor 2",
        ["P0137"] = "Oxygen sensor circuit low voltage bank 1 sensor 2",
        ["P0138"] = "Oxygen sensor circuit high voltage bank 1 sensor 2",
        ["P0139"] = "Oxygen sensor circuit slow response bank 1 sensor 2",
        ["P0140"] = "Oxygen sensor circuit no activity detected bank 1 sensor 2",
        ["P0141"] = "Oxygen sensor heater circuit bank 1 sensor 2",
        ["P0150"] = "Oxygen sensor circuit bank 2 sensor 1",
        ["P0151"] = "Oxygen sensor circuit low voltage bank 2 sensor 1",
        ["P0152"] = "Oxygen sensor circuit high voltage bank 2 sensor 1",
        ["P0153"] = "Oxygen sensor circuit slow response bank 2 sensor 1",
        ["P0154"] = "Oxygen sensor circuit no activity detected bank 2 sensor 1",
        ["P0155"] = "Oxygen sensor heater circuit bank 2 sensor 1",
        ["P0156"] = "Oxygen sensor circuit bank 2 sensor 2",
        ["P0157"] = "Oxygen sensor circuit low voltage bank 2 sensor 2",
        ["P0158"] = "Oxygen sensor circuit high voltage bank 2 sensor 2",
        ["P0159"] = "Oxygen sensor circuit slow response bank 2 sensor 2",
        ["P0160"] = "Oxygen sensor circuit no activity detected bank 2 sensor 2",
        ["P0161"] = "Oxygen sensor heater circuit bank 2 sensor 2",
        ["P0171"] = "System too lean bank 1",
        ["P0172"] = "System too rich bank 1",
        ["P0174"] = "System too lean bank 2",
        ["P0175"] = "System too rich bank 2",
        ["P0201"] = "Injector circuit/open cylinder 1",
        ["P0202"] = "Injector circuit/open cylinder 2",
        ["P0203"] = "Injector circuit/open cylinder 3",
        ["P0204"] = "Injector circuit/open cylinder 4",
        ["P0217"] = "Engine coolant over-temperature condition",
        ["P0325"] = "Knock sensor circuit bank 1",
        ["P0335"] = "Crankshaft position sensor A circuit",
        ["P0340"] = "Camshaft position sensor circuit bank 1 sensor A",
        ["P0351"] = "Ignition coil A primary/secondary circuit",
        ["P0352"] = "Ignition coil B primary/secondary circuit",
        ["P0353"] = "Ignition coil C primary/secondary circuit",
        ["P0354"] = "Ignition coil D primary/secondary circuit",
        ["P0400"] = "Exhaust gas recirculation flow",
        ["P0401"] = "Exhaust gas recirculation flow insufficient detected",
        ["P0402"] = "Exhaust gas recirculation flow excessive detected",
        ["P0403"] = "Exhaust gas recirculation control circuit",
        ["P0300"] = "Random/multiple cylinder misfire",
        ["P0301"] = "Cylinder 1 misfire",
        ["P0302"] = "Cylinder 2 misfire",
        ["P0303"] = "Cylinder 3 misfire",
        ["P0304"] = "Cylinder 4 misfire",
        ["P0327"] = "Knock sensor circuit low input bank 1",
        ["P0328"] = "Knock sensor circuit high input bank 1",
        ["P0420"] = "Catalyst system efficiency below threshold bank 1",
        ["P0430"] = "Catalyst system efficiency below threshold bank 2",
        ["P0440"] = "Evaporative emission control system",
        ["P0441"] = "Evaporative emission system incorrect purge flow",
        ["P0442"] = "Evaporative emission system small leak",
        ["P0443"] = "Evaporative emission system purge control valve circuit",
        ["P0446"] = "Evaporative emission vent control circuit",
        ["P0455"] = "Evaporative emission system gross leak",
        ["P0456"] = "Evaporative emission system very small leak",
        ["P0450"] = "Evaporative emission pressure sensor circuit",
        ["P0451"] = "Evaporative emission pressure sensor range/performance",
        ["P0452"] = "Evaporative emission pressure sensor low input",
        ["P0453"] = "Evaporative emission pressure sensor high input",
        ["P0460"] = "Fuel level sensor circuit",
        ["P0461"] = "Fuel level sensor circuit range/performance",
        ["P0462"] = "Fuel level sensor circuit low input",
        ["P0463"] = "Fuel level sensor circuit high input",
        ["P0500"] = "Vehicle speed sensor",
        ["P0505"] = "Idle air control system",
        ["P0506"] = "Idle control system RPM lower than expected",
        ["P0507"] = "Idle control system RPM higher than expected",
        ["P0560"] = "System voltage",
        ["P0562"] = "System voltage low",
        ["P0563"] = "System voltage high",
        ["P0606"] = "PCM/ECM processor fault",
        ["P0700"] = "Transmission control system MIL request",
        ["P0705"] = "Transmission range sensor circuit",
        ["P0710"] = "Transmission fluid temperature sensor circuit",
        ["P0715"] = "Input/turbine speed sensor circuit",
        ["P0720"] = "Output speed sensor circuit",
        ["P0741"] = "Torque converter clutch circuit performance or stuck off",
        ["P0751"] = "Shift solenoid A performance or stuck off",
        ["P0753"] = "Shift solenoid A electrical",
        ["P0756"] = "Shift solenoid B performance or stuck off",
        ["P0758"] = "Shift solenoid B electrical",
        ["P0771"] = "Shift solenoid E performance or stuck off",
        ["P0773"] = "Shift solenoid E electrical",
        ["P1120"] = "Accelerator pedal position sensor circuit",
        ["P1121"] = "Accelerator pedal position sensor range/performance",
        ["P1125"] = "Throttle control motor circuit",
        ["P1130"] = "Air/fuel ratio sensor circuit range/performance bank 1 sensor 1",
        ["P1133"] = "Air/fuel ratio sensor circuit response bank 1 sensor 1",
        ["P1135"] = "Air/fuel ratio sensor heater circuit bank 1 sensor 1",
        ["P1150"] = "Air/fuel ratio sensor circuit range/performance bank 2 sensor 1",
        ["P1153"] = "Air/fuel ratio sensor circuit response bank 2 sensor 1",
        ["P1155"] = "Air/fuel ratio sensor heater circuit bank 2 sensor 1",
        ["P1604"] = "Startability malfunction",
        ["P1605"] = "Rough idle",
        ["P2111"] = "Throttle actuator control system stuck open",
        ["P2112"] = "Throttle actuator control system stuck closed",
        ["P2118"] = "Throttle actuator control motor current range/performance",
        ["P2120"] = "Throttle/pedal position sensor/switch D circuit",
        ["P2121"] = "Throttle/pedal position sensor/switch D circuit range/performance",
        ["P2122"] = "Throttle/pedal position sensor/switch D circuit low input",
        ["P2123"] = "Throttle/pedal position sensor/switch D circuit high input",
        ["P2135"] = "Throttle/pedal position sensor/switch A/B voltage correlation",
        ["P2195"] = "Oxygen/A-F sensor signal stuck lean bank 1 sensor 1",
        ["P2196"] = "Oxygen/A-F sensor signal stuck rich bank 1 sensor 1",
        ["P2237"] = "Oxygen/A-F sensor positive current control circuit/open bank 1 sensor 1",
        ["P2238"] = "Oxygen/A-F sensor positive current control circuit low bank 1 sensor 1",
        ["P2239"] = "Oxygen/A-F sensor positive current control circuit high bank 1 sensor 1",
        ["C0200"] = "Right front wheel speed sensor signal",
        ["C0205"] = "Left front wheel speed sensor signal",
        ["C0210"] = "Right rear wheel speed sensor signal",
        ["C0215"] = "Left rear wheel speed sensor signal",
        ["C1201"] = "Engine control system malfunction reported to skid control ECU",
        ["C1223"] = "ABS control system malfunction",
        ["C1241"] = "Low or high power supply voltage",
        ["C1246"] = "Master cylinder pressure sensor",
        ["C1252"] = "Brake booster pump motor",
        ["C1336"] = "Zero point calibration of yaw rate/acceleration sensor undone",
        ["U0100"] = "Lost communication with ECM/PCM",
        ["U0121"] = "Lost communication with ABS/skid control module",
        ["U0123"] = "Lost communication with yaw rate sensor module",
        ["U0124"] = "Lost communication with lateral acceleration sensor module",
        ["U0126"] = "Lost communication with steering angle sensor module",
        ["U0155"] = "Lost communication with instrument panel cluster"
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
                    Description = GetDtcDescription(code),
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
        return DecodePidValue(definition, rawResponse, supported, 0x41, readingFactory: (value, raw) => new LivePidReading
        {
            Pid = definition.Pid,
            Name = definition.Name,
            Value = value,
            Unit = definition.Unit,
            Supported = supported ? "Yes" : "Unknown",
            RawResponse = raw
        });
    }

    public static FreezeFrameReading DecodeFreezeFramePid(LivePidDefinition definition, string rawResponse)
    {
        return DecodePidValue(definition, rawResponse, supported: true, 0x42, readingFactory: (value, raw) => new FreezeFrameReading
        {
            Pid = "02" + definition.Pid[2..],
            Name = definition.Name,
            Value = value,
            Unit = definition.Unit,
            RawResponse = raw
        });
    }

    private static T DecodePidValue<T>(
        LivePidDefinition definition,
        string rawResponse,
        bool supported,
        int positiveResponse,
        Func<string, string, T> readingFactory)
    {
        var pid = Convert.ToInt32(definition.Pid[2..], 16);
        var payload = FindPayloads(rawResponse, positiveResponse, pid).FirstOrDefault();

        if (payload is null)
        {
            return readingFactory(supported ? "No data" : "Unsupported", rawResponse);
        }

        var data = payload.Skip(2).ToArray();
        var value = TryConvertLiveValue(definition.Pid, data, out var converted)
            ? converted
            : "Raw " + string.Join(" ", data.Select(valueByte => valueByte.ToString("X2", CultureInfo.InvariantCulture)));

        return readingFactory(value, rawResponse);
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

    public static IReadOnlyList<Mode6TestResult> DecodeMode6Response(string rawResponse)
    {
        var rows = new List<Mode6TestResult>();
        foreach (var payload in FindPayloads(rawResponse, 0x46))
        {
            if (payload.Count < 2)
            {
                continue;
            }

            if (payload.Count >= 9)
            {
                var testId = payload[1];
                var componentId = payload[2];
                var value = ToUInt16(payload[3], payload[4]);
                var minimum = ToUInt16(payload[5], payload[6]);
                var maximum = ToUInt16(payload[7], payload[8]);
                rows.Add(new Mode6TestResult
                {
                    TestId = $"${testId:X2} {DescribeMode6Tid(testId)}",
                    ComponentId = $"${componentId:X2}",
                    Value = value.ToString(CultureInfo.InvariantCulture),
                    Minimum = minimum.ToString(CultureInfo.InvariantCulture),
                    Maximum = maximum.ToString(CultureInfo.InvariantCulture),
                    Status = value >= minimum && value <= maximum ? "Pass" : "Review",
                    RawResponse = rawResponse
                });
                continue;
            }

            rows.Add(new Mode6TestResult
            {
                TestId = $"${payload[1]:X2} {DescribeMode6Tid(payload[1])}",
                ComponentId = "",
                Value = "Raw " + string.Join(" ", payload.Skip(2).Select(valueByte => valueByte.ToString("X2", CultureInfo.InvariantCulture))),
                Minimum = "",
                Maximum = "",
                Status = "Decoded raw",
                RawResponse = rawResponse
            });
        }

        return rows;
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

    private static int ToUInt16(byte high, byte low)
    {
        return (high << 8) + low;
    }

    private static string DescribeMode6Tid(int tid)
    {
        return tid switch
        {
            >= 0x01 and <= 0x0F => "oxygen/air-fuel monitor",
            >= 0x21 and <= 0x2F => "catalyst/EVAP monitor",
            >= 0x31 and <= 0x3F => "EGR/VVT monitor",
            >= 0x51 and <= 0x5F => "misfire monitor",
            _ => "monitor test"
        };
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

    private static string GetDtcDescription(string code)
    {
        if (DtcDescriptions.TryGetValue(code, out var description))
        {
            return description;
        }

        if (code.Length != 5)
        {
            return "Description not in local database";
        }

        var system = code[0] switch
        {
            'P' => "Powertrain",
            'C' => "Chassis",
            'B' => "Body",
            'U' => "Network communication",
            _ => "Unknown system"
        };

        var scope = code[1] switch
        {
            '0' => "generic OBD-II",
            '1' => "manufacturer-specific",
            '2' => "generic or manufacturer-specific",
            '3' => "generic or manufacturer-specific",
            _ => "unknown scope"
        };

        return $"{system} code ({scope}); description not in local database";
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
