namespace TacomaDiag.Models;

public sealed class UsbSerialDeviceInfo
{
    public string Name { get; init; } = "";
    public string AdapterFamily { get; init; } = "";
    public string PnpDeviceId { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string Service { get; init; } = "";
    public string Status { get; init; } = "";
    public int? ConfigManagerErrorCode { get; init; }
    public string PortName { get; init; } = "";

    public bool HasWorkingDriver => ConfigManagerErrorCode == 0 && !string.IsNullOrWhiteSpace(Service);

    public string DriverSummary
    {
        get
        {
            var port = string.IsNullOrWhiteSpace(PortName) ? "no COM port reported" : PortName;
            return ConfigManagerErrorCode switch
            {
                0 when HasWorkingDriver => $"Ready on {port} through service '{Service}'",
                0 => $"Detected on {port}, but no driver service is reported",
                28 => $"Driver missing or failed install (Code 28); {port}",
                null => $"Detected, driver status unknown; {port}",
                _ => $"Detected with Windows device problem code {ConfigManagerErrorCode}; {port}"
            };
        }
    }
}
