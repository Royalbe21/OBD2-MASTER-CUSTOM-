using TacomaDiag.Models;

namespace TacomaDiag.Services;

public static class DiagnosticAdvisor
{
    public static IReadOnlyList<HealthFinding> BuildFindings(
        IEnumerable<DiagnosticTroubleCode> dtcs,
        ReadinessSnapshot? readiness,
        IEnumerable<LivePidReading> liveReadings,
        IEnumerable<FreezeFrameReading> freezeFrameReadings)
    {
        var findings = new List<HealthFinding>();
        var codeList = dtcs.Where(code => code.Code != "None").ToList();

        foreach (var code in codeList)
        {
            findings.Add(new HealthFinding
            {
                Severity = code.Type == "Stored" ? "High" : code.Type == "Pending" ? "Medium" : "Info",
                Area = ClassifyCodeArea(code.Code),
                Finding = $"{code.Code}: {code.Description}",
                NextStep = BuildCodeNextStep(code.Code)
            });
        }

        if (readiness is not null)
        {
            foreach (var monitor in readiness.Monitors.Where(monitor => monitor.Status == "Not ready"))
            {
                findings.Add(new HealthFinding
                {
                    Severity = "Medium",
                    Area = "Readiness",
                    Finding = $"{monitor.Name} monitor is not ready.",
                    NextStep = "Complete the required drive conditions after all codes and repairs are resolved."
                });
            }

            if (readiness.MilOn)
            {
                findings.Add(new HealthFinding
                {
                    Severity = "High",
                    Area = "MIL",
                    Finding = "MIL/check-engine light is reported on.",
                    NextStep = "Prioritize stored DTC diagnosis before clearing codes."
                });
            }
        }

        AddLiveDataFindings(findings, liveReadings);

        if (freezeFrameReadings.Any())
        {
            findings.Add(new HealthFinding
            {
                Severity = "Info",
                Area = "Freeze frame",
                Finding = $"Captured {freezeFrameReadings.Count()} freeze-frame data point(s).",
                NextStep = "Compare freeze-frame load, RPM, coolant temp, and fuel trim against live readings."
            });
        }

        if (findings.Count == 0)
        {
            findings.Add(new HealthFinding
            {
                Severity = "Good",
                Area = "Overall",
                Finding = "No issues are currently visible from scanned data.",
                NextStep = "Run readiness, Mode 6, and a short live-data drive log when the vehicle is connected."
            });
        }

        return findings;
    }

    public static string BuildReadinessGuide(ReadinessSnapshot? readiness)
    {
        if (readiness is null)
        {
            return "Readiness has not been checked yet. Connect to the vehicle, run Check Readiness, then use this guide to target incomplete monitors.";
        }

        var notReady = readiness.Monitors
            .Where(monitor => monitor.Status == "Not ready")
            .Select(monitor => monitor.Name)
            .ToArray();

        if (notReady.Length == 0)
        {
            return "All supported readiness monitors shown by the vehicle are ready.";
        }

        var lines = new List<string>
        {
            "Readiness guide",
            "",
            "Incomplete monitors:",
            string.Join(", ", notReady),
            "",
            "General safe sequence:",
            "1. Start with a cold engine when possible.",
            "2. Let coolant reach normal operating temperature.",
            "3. Drive steadily at neighborhood speed, then highway speed if safe and legal.",
            "4. Include gentle acceleration, steady cruise, and closed-throttle deceleration.",
            "5. Keep fuel level roughly between 1/4 and 3/4 for EVAP monitor attempts.",
            "6. Recheck readiness without clearing codes."
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static void AddLiveDataFindings(ICollection<HealthFinding> findings, IEnumerable<LivePidReading> liveReadings)
    {
        foreach (var reading in liveReadings)
        {
            if (!double.TryParse(reading.Value, out var numeric))
            {
                continue;
            }

            if (reading.Pid is "0105" && numeric < 70)
            {
                findings.Add(new HealthFinding
                {
                    Severity = "Info",
                    Area = "Coolant temp",
                    Finding = $"Coolant temperature is {numeric:0} C.",
                    NextStep = "Warm engine fully before judging fuel trim or readiness behavior."
                });
            }

            if (reading.Pid is "0106" or "0107" && Math.Abs(numeric) >= 12)
            {
                findings.Add(new HealthFinding
                {
                    Severity = Math.Abs(numeric) >= 20 ? "High" : "Medium",
                    Area = "Fuel trim",
                    Finding = $"{reading.Name} is {numeric:0.0}%.",
                    NextStep = "Check for intake leaks, exhaust leaks, MAF contamination, fuel delivery, and A/F sensor behavior."
                });
            }

            if (reading.Pid is "0142" && numeric < 12.2)
            {
                findings.Add(new HealthFinding
                {
                    Severity = "Medium",
                    Area = "Voltage",
                    Finding = $"Control module voltage is {numeric:0.00} V.",
                    NextStep = "Check battery, alternator output, grounds, and adapter connection quality."
                });
            }
        }
    }

    private static string ClassifyCodeArea(string code)
    {
        if (code.StartsWith('C')) return "Chassis/ABS";
        if (code.StartsWith('B')) return "Body";
        if (code.StartsWith('U')) return "Network";
        if (code.StartsWith("P03")) return "Misfire";
        if (code.StartsWith("P04")) return "Emissions";
        if (code.StartsWith("P07")) return "Transmission";
        if (code.StartsWith("P01")) return "Fuel/Air";
        return "Powertrain";
    }

    private static string BuildCodeNextStep(string code)
    {
        if (code.StartsWith("P03")) return "Check misfire freeze-frame, plugs/coils, injector operation, compression, and vacuum leaks.";
        if (code is "P0171" or "P0174") return "Look for unmetered air, MAF contamination, exhaust leaks, low fuel pressure, or A/F sensor bias.";
        if (code.StartsWith("P042") || code.StartsWith("P043")) return "Review Mode 6 catalyst/O2 monitor data before replacing parts.";
        if (code.StartsWith("P044") || code.StartsWith("P045")) return "Inspect gas cap, EVAP hoses, purge valve, vent valve, and tank pressure data.";
        if (code.StartsWith("C")) return "Use module probe and chassis-specific tooling before clearing ABS/chassis faults.";
        if (code.StartsWith("U")) return "Inspect battery voltage, grounds, fuses, and network communication paths.";
        return "Use freeze-frame, live data, and service information to confirm the root cause before clearing codes.";
    }
}
