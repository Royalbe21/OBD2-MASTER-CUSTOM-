namespace TacomaDiag.Models;

public sealed class VehicleModuleTarget
{
    public string Name { get; init; } = "";
    public string System { get; init; } = "";
    public string RequestHeader { get; init; } = "";
    public string ResponseHeader { get; init; } = "";
    public string Notes { get; init; } = "";
    public bool IsPriority { get; init; }
}

public sealed class ModuleScanResult
{
    public string Module { get; init; } = "";
    public string System { get; init; } = "";
    public string RequestHeader { get; init; } = "";
    public string Status { get; init; } = "";
    public string DtcSummary { get; init; } = "";
    public string EcuId { get; init; } = "";
    public string RawResponse { get; init; } = "";
}

public sealed class ModuleDtc
{
    public string Code { get; init; } = "";
    public string Status { get; init; } = "";
    public string RawBytes { get; init; } = "";
}
