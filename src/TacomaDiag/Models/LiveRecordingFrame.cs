namespace TacomaDiag.Models;

public sealed class LiveRecordingFrame
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Rpm { get; init; } = "";
    public string Speed { get; init; } = "";
    public string Coolant { get; init; } = "";
    public string ShortTermFuelTrim { get; init; } = "";
    public string LongTermFuelTrim { get; init; } = "";
    public string Voltage { get; init; } = "";
}
