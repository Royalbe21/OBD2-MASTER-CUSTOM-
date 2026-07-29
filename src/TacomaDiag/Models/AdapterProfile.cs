namespace TacomaDiag.Models;

public sealed class AdapterProfile
{
    public string Name { get; init; } = "";
    public string Command { get; init; } = "";
    public string Description { get; init; } = "";
    public string AdapterFamily { get; init; } = "";
    public string SwitchPosition { get; init; } = "";

    public static IReadOnlyList<AdapterProfile> ElmProtocols { get; } =
    [
        new() { Name = "Auto detect", Command = "ATSP0", Description = "Best first choice for most ELM327 adapters." },
        new() { Name = "CAN 11/500", Command = "ATSP6", Description = "Expected 2008 Toyota Tacoma OBD-II CAN protocol." },
        new() { Name = "CAN 29/500", Command = "ATSP7", Description = "Some newer or heavy-duty CAN networks." },
        new() { Name = "CAN 11/250", Command = "ATSP8", Description = "Some medium-speed CAN networks." },
        new() { Name = "CAN 29/250", Command = "ATSP9", Description = "Some medium-speed extended CAN networks." },
        new()
        {
            Name = "ELM327 USB CH340 auto",
            Command = "ATSP0",
            Description = "Use with the USB ELM327 HS/MS-CAN CH340 adapter; app auto-detects protocol after you set the physical switch to the needed bus.",
            AdapterFamily = "ELM327 USB HS/MS-CAN CH340",
            SwitchPosition = "Set the adapter switch to the bus requested by the workflow before scanning."
        },
        new()
        {
            Name = "Ford/Mazda HS-CAN switch",
            Command = "ATSP6",
            Description = "Use when the adapter switch is set to HS-CAN for powertrain, ABS, and many high-speed Ford/Mazda modules.",
            AdapterFamily = "ELM327 USB HS/MS-CAN CH340",
            SwitchPosition = "Set physical switch to HS-CAN."
        },
        new()
        {
            Name = "Ford/Mazda MS-CAN switch",
            Command = "ATSP8",
            Description = "Use when the adapter switch is set to MS-CAN for many Ford/Mazda body, cluster, HVAC, and comfort modules.",
            AdapterFamily = "ELM327 USB HS/MS-CAN CH340",
            SwitchPosition = "Set physical switch to MS-CAN."
        },
        new() { Name = "ISO 9141-2", Command = "ATSP3", Description = "Older import vehicles before CAN became mandatory." },
        new() { Name = "KWP2000 5-baud", Command = "ATSP4", Description = "Older ISO 14230 vehicles." },
        new() { Name = "KWP2000 fast", Command = "ATSP5", Description = "Older ISO 14230 fast-init vehicles." },
        new() { Name = "J1850 PWM", Command = "ATSP1", Description = "Older Ford-style OBD-II vehicles." },
        new() { Name = "J1850 VPW", Command = "ATSP2", Description = "Older GM-style OBD-II vehicles." }
    ];

    public override string ToString()
    {
        return Name;
    }
}
