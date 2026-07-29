using System.Text;
using TacomaDiag.Models;

namespace TacomaDiag.Services;

public static class DiagnosticWorkflowCatalog
{
    public static IReadOnlyList<DiagnosticWorkflow> Workflows { get; } =
    [
        new()
        {
            Name = "Check engine light triage",
            Objective = "Capture a repeatable baseline before clearing or repairing anything.",
            WhenToUse = "MIL is on, pending codes are suspected, or the truck has a recent drivability complaint.",
            Steps =
            [
                Step(1, "Run Full Scan and save the report.", "Stored, pending, permanent codes, VIN, readiness, and protocol are captured.", "Do not clear codes yet; read freeze frame and note symptoms."),
                Step(2, "Read Freeze Frame.", "RPM, load, coolant, trims, speed, and voltage are captured where supported.", "Use the raw freeze-frame log as evidence even if some PIDs are not supported."),
                Step(3, "Open Advisor and review the highest severity finding first.", "The top finding matches the symptom or DTC family.", "Group codes by system: fuel/air, ignition, catalyst, EVAP, voltage, or network."),
                Step(4, "Save Current Session as the before-repair snapshot.", "History contains a timestamped baseline.", "Export a text or HTML report before clearing codes.")
            ]
        },
        new()
        {
            Name = "Readiness and emissions prep",
            Objective = "Identify incomplete monitors and document the conditions needed before inspection.",
            WhenToUse = "After codes were cleared, battery was disconnected, or inspection readiness is incomplete.",
            Steps =
            [
                Step(1, "Check Readiness.", "MIL is off and required supported monitors show Ready.", "Open Advisor and follow the readiness guide for incomplete monitors."),
                Step(2, "Record a short live-data session during warm idle.", "Coolant, trims, RPM, and voltage look stable.", "Fix low voltage, thermostat, or fuel-trim issues before chasing monitors."),
                Step(3, "Read Mode 6.", "Monitor test rows decode with pass/fail status where the ECU reports limits.", "Treat raw Mode 6 rows as supporting evidence when labels are manufacturer-specific."),
                Step(4, "Save report after every readiness attempt.", "Before/after reports show monitor progress.", "Do not clear codes unless you intentionally want to reset readiness again.")
            ]
        },
        new()
        {
            Name = "Lean/rich fuel trim check",
            Objective = "Use trims and airflow data to separate vacuum leaks, MAF issues, fuel delivery, and exhaust leaks.",
            WhenToUse = "P0171, P0172, rough idle, hesitation, or abnormal fuel economy.",
            Steps =
            [
                Step(1, "Warm the engine fully and record live data.", "Coolant is near operating temperature and closed-loop trims are available.", "Do not judge fuel trims cold."),
                Step(2, "Compare STFT + LTFT at idle and at 2500 RPM.", "Combined trim stays roughly within +/-10%.", "High positive at idle points toward vacuum leak; high positive at load points toward MAF/fuel delivery."),
                Step(3, "Check MAF, MAP, RPM, and throttle for believable values.", "Values change smoothly with throttle.", "Inspect intake boot, MAF contamination, wiring, and exhaust leaks before replacing sensors."),
                Step(4, "Save CSV recording and report.", "The repair note includes before/after trims.", "Use the CSV to compare after cleaning/repair.")
            ]
        },
        new()
        {
            Name = "Misfire and rough idle",
            Objective = "Capture conditions that make a misfire appear and avoid guessing parts.",
            WhenToUse = "P0300-P0304, shake at idle, or stumble under load.",
            Steps =
            [
                Step(1, "Read stored, pending, and permanent codes.", "Misfire cylinder or random misfire code is documented.", "If no code is present, record live data while reproducing the symptom."),
                Step(2, "Read freeze frame and Mode 6.", "Freeze frame gives load/RPM/coolant; Mode 6 may show monitor failures.", "If Mode 6 is raw only, keep the raw data with the report."),
                Step(3, "Check voltage and fuel trims while the symptom occurs.", "Voltage is stable and trims do not indicate a strong lean/rich condition.", "Fix voltage or mixture faults before coils/plugs."),
                Step(4, "Document before/after after plug, coil, injector, compression, or vacuum checks.", "Permanent code eventually clears after drive cycles.", "Do not clear readiness unless you are intentionally starting over.")
            ]
        },
        new()
        {
            Name = "Adapter compatibility audit",
            Objective = "Find the best connection mode and document adapter capability without touching vehicle state.",
            WhenToUse = "Testing ELM327, Bluetooth serial, or J2534 hardware.",
            Steps =
            [
                Step(1, "Select adapter mode and run Self Test.", "Adapter identity, current protocol, protocol number, and 0100 support are logged.", "Try another baud rate or protocol profile if the adapter is unstable."),
                Step(2, "Use Auto Detect first, then the expected Tacoma CAN profile.", "ATDP reports an expected OBD protocol after connect.", "Use the raw terminal to capture adapter errors."),
                Step(3, "Probe Modules only when connected to the vehicle and ready.", "The probe logs responding ECU headers without clearing or writing data.", "Stop if responses are inconsistent or the adapter locks up."),
                Step(4, "Save a session/report for the adapter.", "The history shows connection name and protocol.", "Keep one known-good adapter baseline for comparison.")
            ]
        },
        new()
        {
            Name = "ELM327 USB HS/MS-CAN CH340 adapter setup",
            Objective = "Use the USB ELM327 CH340 adapter safely with Ford/Mazda HS-CAN and MS-CAN switch positions.",
            WhenToUse = "Using a USB ELM327 adapter with a physical HS-CAN/MS-CAN toggle switch and CH340T/CH341 USB serial driver.",
            Steps =
            [
                Step(1, "Install or repair the CH340T/CH341 USB serial driver, then click Refresh.", "The footer or report shows a CH340/CH341 device and a COM port.", "Try another USB port/cable or reinstall the CH340 driver before connecting to a vehicle."),
                Step(2, "For generic OBD-II or powertrain work, set the adapter switch to HS-CAN and select Ford/Mazda HS-CAN switch.", "ATDP reports ISO 15765-4 CAN and 0100 responds.", "Use Auto Detect if the exact protocol is unknown, but keep the switch on HS-CAN for powertrain."),
                Step(3, "For Ford/Mazda body, cluster, HVAC, comfort, and some configuration-related modules, stop scanning and flip the adapter to MS-CAN.", "The workflow and module notes clearly say MS-CAN before those candidates are attempted.", "Do not switch while a scan command is actively running."),
                Step(4, "Save a report before any configuration work in external software.", "The report captures adapter identity, COM port, switch guidance, and responding modules.", "Use external Ford/Mazda tools for configuration writes; this app stays read-only for enhanced modules.")
            ]
        },
        new()
        {
            Name = "Jeep Compass transmission module scan",
            Objective = "Read Chrysler/FCA transmission-controller DTCs and identity data without writing or clearing anything.",
            WhenToUse = "2016 Jeep Compass FWD four-cylinder with CVT/automatic concerns, limp mode, shift issues, or transmission MIL request.",
            Steps =
            [
                Step(1, "Select the 2016 Jeep Compass FWD 4-cylinder profile.", "The profile notes show Chrysler/FCA CAN expectations.", "Do not continue enhanced scanning under the Toyota profile."),
                Step(2, "Connect with Serial/Bluetooth ELM327 or J2534 and force CAN 11/500 if auto-detect is unstable.", "ATDP reports ISO 15765-4 CAN.", "Try J2534 if ELM enhanced module responses are missing or inconsistent."),
                Step(3, "Use Transmission Scan in Mode 6 / Modules.", "TCM candidates respond with DTC records, ECU ID, or a clear unsupported/no-response status.", "If both TCM candidates fail, use a stronger J2534 adapter or Chrysler-capable scan tool."),
                Step(4, "Save the report before clearing or repairing anything.", "Transmission module raw responses are preserved.", "MPPS should not be used for diagnosis except as an external ECU/TCU file tool where appropriate.")
            ]
        },
        new()
        {
            Name = "Hyundai Elantra module scan",
            Objective = "Read Hyundai/Kia engine, transmission, ABS/ESC, SRS, body, and steering candidates without writing or clearing anything.",
            WhenToUse = "2014 Hyundai Elantra 2.0L with transmission, drivability, ABS, airbag, or body-module concerns.",
            Steps =
            [
                Step(1, "Select the 2014 Hyundai Elantra 2.0L profile.", "The profile notes show Hyundai/Kia CAN expectations.", "Do not continue enhanced scanning under an unrelated profile."),
                Step(2, "Connect with Serial/Bluetooth ELM327 or J2534 and force CAN 11/500 if auto-detect is unstable.", "ATDP reports ISO 15765-4 CAN.", "Use a J2534 interface if ELM module responses are missing or inconsistent."),
                Step(3, "Use Transmission Scan first for transmission complaints.", "The TCM candidates return DTC records, ECU ID, or a clear unsupported/no-response status.", "If both TCM candidates fail, try Enhanced Module Scan with a stronger adapter."),
                Step(4, "Use Enhanced Module Scan for all candidates and save the report.", "Raw responses are preserved for later review.", "Do not use clear/coding/programming operations until the fault is documented.")
            ]
        },
        new()
        {
            Name = "Dodge/Freightliner Sprinter diesel van scan",
            Objective = "Capture emissions, engine, transmission, ABS/ESP, SRS, and body-module candidates on Mercedes-derived Sprinter diesel vans without writing or clearing enhanced modules.",
            WhenToUse = "Dodge or Freightliner Sprinter diesel vans with glow plug, turbo/boost, limp mode, transmission, ABS/ESP, SRS, or body-module concerns.",
            Steps =
            [
                Step(1, "Select the Dodge/Freightliner Sprinter diesel van profile.", "The profile notes show Sprinter/Mercedes-derived diagnostics and protocol cautions.", "Do not use the generic Chrysler profile for Sprinter-specific diesel/module work."),
                Step(2, "Connect with auto-detect first; on 2007+ vans try CAN 11/500 if needed.", "Generic OBD-II responds before enhanced scanning.", "Early vans may need K-line/ISO support or Sprinter-capable tooling if CAN does not respond."),
                Step(3, "Run Transmission Scan for limp-mode or shift complaints.", "Engine and transmission candidates return DTCs, ECU ID, or a clear unsupported/no-response status.", "If ELM responses are inconsistent, retry with a stronger J2534 adapter or Sprinter-capable scan tool."),
                Step(4, "Run Enhanced Module Scan and save the report before clearing anything.", "Raw ABS/ESP, SRS, body, cluster, and HVAC candidates are preserved.", "Do not clear SRS/ABS or perform adaptations without verified Sprinter tooling.")
            ]
        },
        new()
        {
            Name = "Ram ProMaster EcoDiesel van scan",
            Objective = "Capture read-only FCA/Ram diesel van engine, transmission, chassis, safety, and body candidates with diesel aftertreatment notes.",
            WhenToUse = "Ram ProMaster 3.0L EcoDiesel vans with DPF/DEF/SCR, limp mode, transmission, ABS, airbag, or body-module concerns.",
            Steps =
            [
                Step(1, "Select the Ram ProMaster 3.0L EcoDiesel van profile.", "The profile loads ProMaster diesel priorities and warnings.", "Do not perform forced regens, relearns, or resets from this prototype."),
                Step(2, "Connect with Serial/Bluetooth ELM327 or J2534 and confirm CAN 11/500.", "Generic OBD-II responds and VIN/readiness can be captured.", "Try J2534 if the ELM adapter drops frames or enhanced candidates do not respond."),
                Step(3, "Use Transmission Scan for transmission or limp-mode complaints.", "TCM candidates respond with DTCs, ECU ID, or unsupported/no-response status.", "If TCM candidates fail, capture the report and retry with FCA-enhanced tooling."),
                Step(4, "Use Enhanced Module Scan and save the report.", "Diesel powertrain and body/chassis raw responses are preserved.", "Aftertreatment resets and service routines require verified FCA/Ram-capable tools.")
            ]
        },
        new()
        {
            Name = "Chevrolet Express / GMC Savana van scan",
            Objective = "Capture GM van engine, transmission, ABS, airbag, BCM, cluster, and HVAC candidates with diesel/gas protocol cautions.",
            WhenToUse = "Chevrolet Express or GMC Savana vans with Duramax diesel, gas powertrain, transmission, ABS, airbag, body, or HVAC concerns.",
            Steps =
            [
                Step(1, "Select the Chevrolet Express / GMC Savana van profile.", "The profile loads GM van candidates and protocol notes.", "Older vans may use VPW/Class 2 instead of CAN; let the adapter auto-detect first."),
                Step(2, "Connect and confirm the detected protocol before enhanced scanning.", "Generic OBD-II responds and the raw terminal logs the protocol.", "If older VPW/Class 2 communication fails, try another adapter profile or a GM-capable scanner."),
                Step(3, "Use Transmission Scan for shift, tow/haul, or limp-mode concerns.", "TCM candidates return DTCs, ECU ID, or unsupported/no-response status.", "Some GM module data needs enhanced definitions even when generic OBD works."),
                Step(4, "Use Enhanced Module Scan and save the report.", "EBCM, SDM, BCM, IPC, HVAC, and fallback candidates are documented.", "Do not clear safety/chassis codes until the original faults and freeze-frame context are saved.")
            ]
        },
        new()
        {
            Name = "Generic manufacturer module scan",
            Objective = "Try safe read-only CAN module probing across common manufacturer families.",
            WhenToUse = "A vehicle is not covered by a specific profile but uses CAN OBD-II.",
            Steps =
            [
                Step(1, "Select the closest manufacturer profile or Generic OBD-II CAN profile.", "The selected family loads the safest known module target set.", "If unsure, start with Generic OBD-II CAN."),
                Step(2, "Connect and confirm CAN 11/500 or the vehicle's detected CAN protocol.", "Generic OBD-II requests respond before enhanced scanning.", "Fix adapter/protocol issues before module scanning."),
                Step(3, "Run Enhanced Module Scan read-only.", "Responding modules are captured with raw responses and decoded DTCs where possible.", "No response does not prove a module is absent; it may need manufacturer-specific addressing."),
                Step(4, "Save the report and raw logs.", "The scan result can guide which professional/J2534 tool is needed next.", "Do not assume unsupported enhanced responses mean the vehicle has no faults.")
            ]
        }
    ];

    public static string BuildWorkflowText(DiagnosticWorkflow workflow)
    {
        var builder = new StringBuilder();
        builder.AppendLine(workflow.Name);
        builder.AppendLine(new string('=', workflow.Name.Length));
        builder.AppendLine($"Objective: {workflow.Objective}");
        builder.AppendLine($"Use when: {workflow.WhenToUse}");
        builder.AppendLine();

        foreach (var step in workflow.Steps)
        {
            builder.AppendLine($"{step.Number}. {step.Action}");
            builder.AppendLine($"   Pass: {step.PassCondition}");
            builder.AppendLine($"   If not: {step.NextIfFail}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static WorkflowStep Step(int number, string action, string passCondition, string nextIfFail)
    {
        return new WorkflowStep
        {
            Number = number,
            Action = action,
            PassCondition = passCondition,
            NextIfFail = nextIfFail
        };
    }
}
