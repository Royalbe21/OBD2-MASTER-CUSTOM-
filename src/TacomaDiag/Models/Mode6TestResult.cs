namespace TacomaDiag.Models;

public sealed class Mode6TestResult
{
    public string TestId { get; init; } = "";
    public string ComponentId { get; init; } = "";
    public string Value { get; init; } = "";
    public string Minimum { get; init; } = "";
    public string Maximum { get; init; } = "";
    public string Status { get; init; } = "";
    public string RawResponse { get; init; } = "";
}
