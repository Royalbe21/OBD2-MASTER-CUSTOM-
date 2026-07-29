namespace TacomaDiag.Models;

public sealed class AdapterWizardStep
{
    public string Step { get; init; } = "";
    public string Status { get; init; } = "";
    public string Detail { get; init; } = "";
    public string RawResponse { get; init; } = "";
}
