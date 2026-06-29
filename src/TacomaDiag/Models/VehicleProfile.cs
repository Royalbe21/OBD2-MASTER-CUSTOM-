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

    public static VehicleProfile ToyotaTacoma2008Base2TrFe { get; } = new()
    {
        Name = "2008 Toyota Tacoma Base 2.7L",
        Year = "2008",
        Make = "Toyota",
        Model = "Tacoma Base",
        Engine = "2TR-FE 2.7L inline-four",
        ExpectedProtocol = "ISO 15765-4 CAN, usually 11-bit / 500 kbps",
        Notes = "Prototype is read-focused except for confirmed OBD-II code clearing. Enhanced Toyota module probing is limited by ELM327 capability."
    };
}
