namespace TacomaDiag.Models;

public sealed class FreezeFrameReading
{
    public string Pid { get; init; } = "";
    public string Name { get; init; } = "";
    public string Value { get; init; } = "";
    public string Unit { get; init; } = "";
    public string RawResponse { get; init; } = "";
}
