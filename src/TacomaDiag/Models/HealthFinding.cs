namespace TacomaDiag.Models;

public sealed class HealthFinding
{
    public string Severity { get; init; } = "";
    public string Area { get; init; } = "";
    public string Finding { get; init; } = "";
    public string NextStep { get; init; } = "";
}
