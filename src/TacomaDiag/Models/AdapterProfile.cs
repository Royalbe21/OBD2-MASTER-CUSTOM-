namespace TacomaDiag.Models;

public sealed class AdapterProfile
{
    public string Name { get; init; } = "";
    public string Command { get; init; } = "";
    public string Description { get; init; } = "";

    public static IReadOnlyList<AdapterProfile> ElmProtocols { get; } =
    [
        new() { Name = "Auto detect", Command = "ATSP0", Description = "Best first choice for most ELM327 adapters." },
        new() { Name = "CAN 11/500", Command = "ATSP6", Description = "Expected 2008 Toyota Tacoma OBD-II CAN protocol." },
        new() { Name = "CAN 29/500", Command = "ATSP7", Description = "Some newer or heavy-duty CAN networks." },
        new() { Name = "CAN 11/250", Command = "ATSP8", Description = "Some medium-speed CAN networks." },
        new() { Name = "CAN 29/250", Command = "ATSP9", Description = "Some medium-speed extended CAN networks." },
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
