namespace TacomaDiag.Models;

public sealed record LivePidDefinition(string Pid, string Name, string Unit);

public sealed class LivePidReading
{
    public string Pid { get; init; } = "";
    public string Name { get; init; } = "";
    public string Value { get; init; } = "";
    public string Unit { get; init; } = "";
    public string Supported { get; init; } = "";
    public string RawResponse { get; init; } = "";
}
