using System.Text;
using TacomaDiag.Models;

namespace TacomaDiag.Services;

public static class ScanResultAnalyzer
{
    public static IReadOnlyList<ScanAnalysisFinding> BuildFindings(
        VehicleProfile profile,
        string adapterMode,
        string adapterProfile,
        string connectionName,
        string protocol,
        IEnumerable<AdapterWizardStep> adapterWizardRows,
        IEnumerable<VehicleDiscoveryStep> vehicleDiscoveryRows,
        IEnumerable<ModuleScanResult> moduleScanRows,
        IEnumerable<DiagnosticTroubleCode> dtcs,
        ReadinessSnapshot? readiness)
    {
        var findings = new List<ScanAnalysisFinding>();
        var adapterRows = adapterWizardRows.ToList();
        var discoveryRows = vehicleDiscoveryRows.ToList();
        var modules = moduleScanRows.ToList();
        var codeList = dtcs.Where(code => code.Code != "None").ToList();

        AddAdapterFindings(findings, adapterMode, adapterProfile, connectionName, protocol, adapterRows);
        AddVehicleFindings(findings, discoveryRows, codeList, readiness);
        AddModuleFindings(findings, profile, adapterMode, adapterProfile, modules);

        if (findings.Count == 0)
        {
            findings.Add(Finding(
                "Info",
                "Baseline",
                "No scan evidence is available yet.",
                "Run Adapter Wizard, then Vehicle Discovery.",
                "Create a vehicle baseline before clearing codes or running manufacturer-specific module scans."));
        }

        var highestSeverity = findings.Any(finding => finding.Severity is "High")
            ? "High"
            : findings.Any(finding => finding.Severity is "Medium")
                ? "Medium"
                : findings.Any(finding => finding.Severity is "Review")
                    ? "Review"
                    : "Good";

        findings.Insert(0, Finding(
            highestSeverity,
            "Session summary",
            BuildSessionDiagnosis(connectionName, protocol, modules, codeList, readiness),
            BuildSessionEvidence(adapterMode, adapterProfile, modules, discoveryRows),
            BuildSessionRecommendation(profile, adapterMode, adapterProfile, modules)));

        return findings;
    }

    public static string BuildNarrative(IEnumerable<ScanAnalysisFinding> findings)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Scan Result Analyzer");
        builder.AppendLine("====================");

        foreach (var finding in findings)
        {
            builder.AppendLine($"[{finding.Severity}] {finding.Area}");
            builder.AppendLine($"Diagnosis: {finding.Diagnosis}");
            builder.AppendLine($"Evidence: {finding.Evidence}");
            builder.AppendLine($"Next: {finding.Recommendation}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AddAdapterFindings(
        ICollection<ScanAnalysisFinding> findings,
        string adapterMode,
        string adapterProfile,
        string connectionName,
        string protocol,
        IReadOnlyList<AdapterWizardStep> adapterRows)
    {
        if (connectionName.Contains("Disconnected", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(Finding("High", "Adapter", "No active adapter connection is recorded.", connectionName, "Connect the adapter or run Adapter Wizard before vehicle scans."));
            return;
        }

        if (adapterRows.Any(row => row.Status is "Fail"))
        {
            findings.Add(Finding("High", "Adapter", "Adapter wizard reported a failed step.", SummarizeRows(adapterRows.Where(row => row.Status is "Fail")), "Fix driver, COM port, baud rate, J2534 DLL, or adapter power before scanning modules."));
        }

        if (adapterRows.Any(row => row.Status is "Review" && row.Step.Contains("ELM", StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(Finding("Medium", "Adapter", "ELM command responses need review.", SummarizeRows(adapterRows.Where(row => row.Status is "Review")), "Try another baud rate, avoid other apps using the same COM port, or try J2534 for enhanced modules."));
        }

        if (string.IsNullOrWhiteSpace(protocol))
        {
            findings.Add(Finding("Medium", "Protocol", "No OBD protocol has been confirmed in this session.", $"Mode {adapterMode}; profile {adapterProfile}", "Run Adapter Wizard or Vehicle Discovery and confirm ATDP/ATDPN before module mapping."));
        }
        else if (protocol.Contains("CAN", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(Finding("Good", "Protocol", "CAN protocol is confirmed.", protocol, "Continue with Vehicle Discovery and the focused manufacturer scan for this vehicle."));
        }
    }

    private static void AddVehicleFindings(
        ICollection<ScanAnalysisFinding> findings,
        IReadOnlyList<VehicleDiscoveryStep> discoveryRows,
        IReadOnlyList<DiagnosticTroubleCode> dtcs,
        ReadinessSnapshot? readiness)
    {
        var ecuResponse = discoveryRows.FirstOrDefault(row => row.Area.Contains("Vehicle ECU", StringComparison.OrdinalIgnoreCase) || row.Area.Contains("Supported PIDs", StringComparison.OrdinalIgnoreCase));
        if (ecuResponse is not null && ecuResponse.Status is "Fail" or "Review")
        {
            findings.Add(Finding("High", "Vehicle response", "The vehicle did not clearly answer generic OBD requests.", ecuResponse.Detail, "Check ignition ON, vehicle connection, selected protocol, and HS/MS-CAN switch position."));
        }

        if (dtcs.Count > 0)
        {
            findings.Add(Finding(
                dtcs.Any(code => code.Type == "Stored") ? "High" : "Medium",
                "DTCs",
                $"{dtcs.Count} diagnostic trouble code(s) are present.",
                string.Join(", ", dtcs.Select(code => $"{code.Type} {code.Code}")),
                "Save the baseline and freeze-frame data before clearing codes."));
        }

        if (readiness is not null)
        {
            var notReady = readiness.Monitors.Count(monitor => monitor.Status == "Not ready");
            if (notReady > 0)
            {
                findings.Add(Finding("Medium", "Readiness", $"{notReady} readiness monitor(s) are incomplete.", $"MIL {(readiness.MilOn ? "on" : "off")}; DTC count {readiness.ConfirmedDtcCount}", "Do not clear codes unless intentionally restarting readiness; complete the required drive cycle after repairs."));
            }
        }
    }

    private static void AddModuleFindings(
        ICollection<ScanAnalysisFinding> findings,
        VehicleProfile profile,
        string adapterMode,
        string adapterProfile,
        IReadOnlyList<ModuleScanResult> modules)
    {
        if (modules.Count == 0)
        {
            findings.Add(Finding("Info", "Modules", "No module map has been captured yet.", profile.Name, "Run Vehicle Discovery or a focused manufacturer scan."));
            return;
        }

        var responded = modules.Where(module => module.Status.Equals("Responded", StringComparison.OrdinalIgnoreCase)).ToList();
        var noResponse = modules.Where(module => !module.Status.Equals("Responded", StringComparison.OrdinalIgnoreCase)).ToList();
        var negative = modules.Where(module => LooksLikeNegativeResponse(module.RawResponse)).ToList();
        var canErrors = modules.Where(module => LooksLikeCanOrAdapterError(module.RawResponse)).ToList();
        var msCan = modules.Where(module => module.Bus.Equals("MS-CAN", StringComparison.OrdinalIgnoreCase)).ToList();
        var msCanResponded = msCan.Any(module => module.Status.Equals("Responded", StringComparison.OrdinalIgnoreCase));

        if (responded.Count > 0)
        {
            findings.Add(Finding("Good", "Module map", $"{responded.Count} module candidate(s) responded.", string.Join(", ", responded.Take(8).Select(module => $"{module.Module} {module.RequestHeader}")), "Use responding modules for targeted diagnosis and save the report as a known-good/bad baseline."));
        }

        if (noResponse.Count > 0)
        {
            findings.Add(Finding("Review", "No-response modules", $"{noResponse.Count} module candidate(s) did not respond.", string.Join(", ", noResponse.Take(8).Select(module => $"{module.Module} {module.Bus}")), "No response can mean module not equipped, wrong bus, unsupported service, gateway filtering, or adapter limitations."));
        }

        if (negative.Count > 0)
        {
            findings.Add(Finding("Review", "Negative responses", $"{negative.Count} module candidate(s) returned diagnostic negative-response data.", string.Join(", ", negative.Take(6).Select(module => module.Module)), "This usually means the module answered but rejected the service; try identity-only reads, J2534, or manufacturer-enhanced definitions."));
        }

        if (canErrors.Count > 0)
        {
            findings.Add(Finding("Medium", "Adapter/protocol errors", $"{canErrors.Count} module row(s) include CAN/adapter error text.", string.Join(", ", canErrors.Take(6).Select(module => module.Module)), "Retry with ignition ON, shorter target list, forced CAN 11/500, adjusted timeout, or a stronger adapter/J2534 interface."));
        }

        if (msCan.Count > 0 && !msCanResponded && !adapterProfile.Contains("MS-CAN", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(Finding("Medium", "Ford/Mazda MS-CAN", "MS-CAN candidates were present but the selected profile is not MS-CAN.", adapterProfile, "Set the physical adapter switch to MS-CAN, choose Ford/Mazda MS-CAN switch, and rerun only MS-CAN targets."));
        }

        if (adapterMode.Contains("Serial", StringComparison.OrdinalIgnoreCase) && noResponse.Count > responded.Count && profile.ManufacturerFamily is not "Generic")
        {
            findings.Add(Finding("Info", "Adapter capability", "Most enhanced module candidates did not respond through serial ELM mode.", $"Responded {responded.Count}; no response {noResponse.Count}", "Generic OBD may still work, but deeper module coverage may need J2534 or manufacturer-capable tooling."));
        }
    }

    private static string BuildSessionDiagnosis(string connectionName, string protocol, IReadOnlyList<ModuleScanResult> modules, IReadOnlyList<DiagnosticTroubleCode> dtcs, ReadinessSnapshot? readiness)
    {
        var responded = modules.Count(module => module.Status.Equals("Responded", StringComparison.OrdinalIgnoreCase));
        var notReady = readiness?.Monitors.Count(monitor => monitor.Status == "Not ready") ?? 0;
        return $"Connection {connectionName}; protocol {(string.IsNullOrWhiteSpace(protocol) ? "not confirmed" : protocol)}; modules responded {responded}/{modules.Count}; DTCs {dtcs.Count}; not-ready monitors {notReady}.";
    }

    private static string BuildSessionEvidence(string adapterMode, string adapterProfile, IReadOnlyList<ModuleScanResult> modules, IReadOnlyList<VehicleDiscoveryStep> discoveryRows)
    {
        var confirmedBuses = modules
            .Where(module => module.Status.Equals("Responded", StringComparison.OrdinalIgnoreCase))
            .Select(module => string.IsNullOrWhiteSpace(module.Bus) ? "CAN" : module.Bus)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var discoverySummary = discoveryRows.Count == 0 ? "no vehicle discovery rows" : $"{discoveryRows.Count} discovery row(s)";
        return $"Adapter mode {adapterMode}; profile {adapterProfile}; confirmed buses {(confirmedBuses.Length == 0 ? "none" : string.Join(", ", confirmedBuses))}; {discoverySummary}.";
    }

    private static string BuildSessionRecommendation(VehicleProfile profile, string adapterMode, string adapterProfile, IReadOnlyList<ModuleScanResult> modules)
    {
        if (modules.Any(module => module.Bus.Equals("MS-CAN", StringComparison.OrdinalIgnoreCase)) && !adapterProfile.Contains("MS-CAN", StringComparison.OrdinalIgnoreCase))
        {
            return "For Ford/Mazda body modules, switch the adapter to MS-CAN and rerun the Ford/Mazda MS scan.";
        }

        if (modules.Count == 0)
        {
            return "Run Vehicle Discovery first, then the manufacturer-specific scan that matches the selected profile.";
        }

        if (adapterMode.Contains("Serial", StringComparison.OrdinalIgnoreCase) && modules.Count(module => module.Status.Equals("Responded", StringComparison.OrdinalIgnoreCase)) == 0)
        {
            return "Try forced CAN 11/500, verify ignition ON, then try J2534 or a stronger adapter if enhanced modules still do not respond.";
        }

        return $"Save this baseline, then diagnose the highest-priority {profile.ManufacturerFamily} responding module or DTC first.";
    }

    private static ScanAnalysisFinding Finding(string severity, string area, string diagnosis, string evidence, string recommendation)
    {
        return new ScanAnalysisFinding
        {
            Severity = severity,
            Area = area,
            Diagnosis = diagnosis,
            Evidence = evidence,
            Recommendation = recommendation
        };
    }

    private static string SummarizeRows(IEnumerable<AdapterWizardStep> rows)
    {
        return string.Join("; ", rows.Take(5).Select(row => $"{row.Step}: {row.Detail}"));
    }

    private static bool LooksLikeNegativeResponse(string raw)
    {
        var compact = Normalize(raw);
        return compact.Contains("7F", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("SERVICE NOT SUPPORTED", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("CONDITIONS NOT CORRECT", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("SECURITY", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeCanOrAdapterError(string raw)
    {
        return raw.Contains("CAN ERROR", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("BUS ERROR", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("UNABLE TO CONNECT", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("BUFFER", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("STOPPED", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string raw)
    {
        return raw.Replace(" ", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal)
            .ToUpperInvariant();
    }
}
