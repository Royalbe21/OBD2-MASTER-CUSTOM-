using TacomaDiag.Models;
using TacomaDiag.Services;

var failures = 0;
var checks = 0;
void Check(string name, string expected, string actual)
{
    checks++;
    var pass = actual == expected;
    if (!pass) failures++;
    Console.WriteLine($"{(pass ? "PASS" : "FAIL")} {name}: expected [{expected}], actual [{actual}]");
}

// Vendor example, ELM327 datasheet p.42, normal formatted CAN output.
var canVin = "014\r0: 49 02 01 31 44 34\r1: 47 50 30 30 52 35 35\r2: 42 31 32 33 34 35 36";
Check("VIN from formatted CAN response", "1D4GP00R55B123456", ObdDecoder.DecodeVin(canVin));
var legacyVin = "49 02 01 00 00 00 31\r49 02 02 44 34 47 50\r49 02 03 30 30 52 35\r49 02 04 35 42 31 32\r49 02 05 33 34 35 36";
Check("VIN from J1850 response", "1D4GP00R55B123456", ObdDecoder.DecodeVin(legacyVin));
var joinedVin = "49 02 01 31 44 34 47 50 30 30 52 35 35 42 31 32 33 34 35 36";
Check("VIN from assembled payload", "1D4GP00R55B123456", ObdDecoder.DecodeVin(joinedVin));

var rpm = ObdDecoder.LivePidDefinitions.Single(d => d.Pid == "010C");
var coolant = ObdDecoder.LivePidDefinitions.Single(d => d.Pid == "0105");
Check("Live RPM", "1000", ObdDecoder.DecodeLivePid(rpm, "41 0C 0F A0", true).Value);
Check("Freeze-frame RPM with frame 00", "1000", ObdDecoder.DecodeFreezeFramePid(rpm, "42 0C 00 0F A0").Value);
Check("Live coolant", "83", ObdDecoder.DecodeLivePid(coolant, "41 05 7B", true).Value);
Check("Freeze-frame coolant with frame 00", "83", ObdDecoder.DecodeFreezeFramePid(coolant, "42 05 00 7B").Value);

var code = ObdDecoder.DecodeDtcResponse("43 01 71 00 00 00 00", "Stored");
Check("Legacy DTC P0171", "P0171", code.Single().Code);
var readiness = ObdDecoder.DecodeReadiness("41 01 00 07 00 00");
Check("Readiness MIL off", "False", readiness.MilOn.ToString());

// A completed scan is required to support an overall healthy result.
var unscanned = DiagnosticAdvisor.BuildFindings([], null, [], []);
Check("Unscanned advisor overall status", "Info", unscanned.Single().Severity);
Check("CAN counted DTC", "P0171", ObdDecoder.DecodeDtcResponse("43 01 01 71", "Stored").Single().Code);
Check("CAN zero codes", "0", ObdDecoder.DecodeDtcResponse("43 00", "Stored").Count.ToString());
foreach (var invalid in new[] { "NO DATA", "CAN ERROR", "43", "43 02 01 71" })
{
    try
    {
        ObdDecoder.DecodeDtcResponse(invalid, "Stored");
        Check(invalid, "incomplete", "accepted");
    }
    catch (FormatException)
    {
        Check(invalid, "incomplete", "incomplete");
    }
}
foreach (var (raw, assembled, expected) in new[] {
    ("7E8 06 41 00 BE 3F A8 13", false, true), ("7E9 06 41 00 BE 3F A8 13", false, false),
    ("7E8 41 00 BE 3F A8 13", true, true), ("ERROR: timeout", false, false),
    ("7E8 06 41 00", false, false), ("7E8 03 7F 01 12", false, false) })
    Check($"Discovery {raw}", expected.ToString(), ModuleReply.HasPositive(raw, "7E8", assembled, 0x41, 0).ToString());
Console.WriteLine($"Review checks: {checks - failures}/{checks} passed; {failures} failures reproduced.");
Environment.ExitCode = failures == 0 ? 0 : 1;
