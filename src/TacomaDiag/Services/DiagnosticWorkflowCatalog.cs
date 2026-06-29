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
