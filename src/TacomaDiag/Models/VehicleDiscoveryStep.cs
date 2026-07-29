namespace TacomaDiag.Models;

public sealed class VehicleDiscoveryStep
{
    public string Area { get; init; } = "";
    public string Status { get; init; } = "";
    public string Detail { get; init; } = "";
    public string RawResponse { get; init; } = "";
}
