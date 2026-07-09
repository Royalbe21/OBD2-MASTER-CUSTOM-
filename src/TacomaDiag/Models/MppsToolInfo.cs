namespace TacomaDiag.Models;

public sealed class MppsToolInfo
{
    public string Name { get; init; } = "";
    public string ExecutablePath { get; init; } = "";
    public string Source { get; init; } = "";
    public string Status { get; init; } = "";

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Name) ? ExecutablePath : Name;
    }
}
