namespace TacomaDiag.Models;

public sealed class VehicleProfile
{
    public string Name { get; init; } = "";
    public string Year { get; init; } = "";
    public string Make { get; init; } = "";
    public string Model { get; init; } = "";
    public string Engine { get; init; } = "";
    public string ExpectedProtocol { get; init; } = "";
    public string Notes { get; init; } = "";
    public string ManufacturerFamily { get; init; } = "";

    public static IReadOnlyList<VehicleProfile> BuiltInProfiles => [ToyotaTacoma2008Base2TrFe, JeepCompass2016FwdFourCylinder];

    public static VehicleProfile ToyotaTacoma2008Base2TrFe { get; } = new()
    {
        Name = "2008 Toyota Tacoma Base 2.7L",
        Year = "2008",
        Make = "Toyota",
        Model = "Tacoma Base",
        Engine = "2TR-FE 2.7L inline-four",
        ExpectedProtocol = "ISO 15765-4 CAN, usually 11-bit / 500 kbps",
        Notes = "Prototype is read-focused except for confirmed OBD-II code clearing. Enhanced Toyota module probing is limited by ELM327 capability.",
        ManufacturerFamily = "Toyota"
    };

    public static VehicleProfile JeepCompass2016FwdFourCylinder { get; } = new()
    {
        Name = "2016 Jeep Compass FWD 4-cylinder",
        Year = "2016",
        Make = "Jeep",
        Model = "Compass FWD",
        Engine = "2.0L or 2.4L inline-four",
        ExpectedProtocol = "ISO 15765-4 CAN, usually 11-bit / 500 kbps",
        Notes = "Chrysler/FCA enhanced module coverage depends on adapter support and module addressing. ELM327 can attempt read-only OBD/UDS scans; fuller coverage normally requires a capable J2534 interface and Chrysler-enhanced definitions.",
        ManufacturerFamily = "Chrysler"
    };

    public override string ToString() => Name;
}
