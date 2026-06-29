namespace TacomaDiag.Models;

public sealed class DiagnosticTroubleCode
{
    public string Type { get; init; } = "";
    public string Code { get; init; } = "";
    public string Description { get; init; } = "";
    public string RawResponse { get; init; } = "";
}
