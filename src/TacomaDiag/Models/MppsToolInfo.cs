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

public sealed class MppsUsbDeviceInfo
{
    public string Name { get; init; } = "";
    public string PnpDeviceId { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string Service { get; init; } = "";
    public string Status { get; init; } = "";
    public int? ConfigManagerErrorCode { get; init; }
    public IReadOnlyList<string> HardwareIds { get; init; } = [];

    public bool HasWorkingDriver => ConfigManagerErrorCode == 0 && !string.IsNullOrWhiteSpace(Service);

    public string DriverSummary
    {
        get
        {
            return ConfigManagerErrorCode switch
            {
                0 when HasWorkingDriver => $"Ready through Windows driver service '{Service}'",
                0 => "Detected, but no driver service is reported",
                28 => "Driver missing or failed install (Code 28)",
                null => "Detected, driver status unknown",
                _ => $"Detected with Windows device problem code {ConfigManagerErrorCode}"
            };
        }
    }

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Name) ? PnpDeviceId : Name;
    }
}
