namespace TacomaDiag.Models;

public sealed class ScanAnalysisFinding
{
    public string Severity { get; init; } = "";
    public string Area { get; init; } = "";
    public string Diagnosis { get; init; } = "";
    public string Evidence { get; init; } = "";
    public string Recommendation { get; init; } = "";
}
