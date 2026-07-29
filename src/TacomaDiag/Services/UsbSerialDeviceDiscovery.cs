using System.Management;
using System.Text.RegularExpressions;
using TacomaDiag.Models;

namespace TacomaDiag.Services;

public static partial class UsbSerialDeviceDiscovery
{
    private static readonly KnownSerialAdapter[] KnownAdapters =
    [
        new("WCH CH340/CH341", @"USB\VID_1A86&PID_7523", "CH340", "wch"),
        new("WCH CH340/CH341", @"USB\VID_1A86&PID_5523", "CH341", "wch"),
        new("WCH CH9102", @"USB\VID_1A86&PID_55D4", "CH910", "wch"),
        new("FTDI FT232/FT2232", @"USB\VID_0403&PID_6001", "FT232", "ftdi"),
        new("FTDI FT232/FT2232", @"USB\VID_0403&PID_6010", "FT2232", "ftdi"),
        new("FTDI FT231X", @"USB\VID_0403&PID_6015", "FT231", "ftdi"),
        new("Silicon Labs CP210x", @"USB\VID_10C4&PID_EA60", "CP210", "silicon labs"),
        new("Prolific PL2303", @"USB\VID_067B&PID_2303", "PL2303", "prolific"),
        new("Microchip MCP2200", @"USB\VID_04D8&PID_00DD", "MCP2200", "microchip")
    ];

    public static IReadOnlyList<UsbSerialDeviceInfo> FindCh340Devices()
    {
        return FindKnownAdapterDevices()
            .Where(device => device.AdapterFamily.Contains("CH340", StringComparison.OrdinalIgnoreCase) ||
                device.AdapterFamily.Contains("CH341", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public static IReadOnlyList<UsbSerialDeviceInfo> FindKnownAdapterDevices()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var devices = new List<UsbSerialDeviceInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\CIMV2",
                "SELECT * FROM Win32_PnPEntity WHERE PNPDeviceID LIKE 'USB%' OR PNPDeviceID LIKE 'BTHENUM%' OR Name LIKE '%(COM%'");

            foreach (ManagementObject device in searcher.Get().Cast<ManagementObject>())
            {
                var name = ReadManagementString(device, "Name");
                var pnpDeviceId = ReadManagementString(device, "PNPDeviceID");
                var manufacturer = ReadManagementString(device, "Manufacturer");
                var service = ReadManagementString(device, "Service");
                var adapterFamily = IdentifyAdapterFamily(name, pnpDeviceId, manufacturer, service);
                if (string.IsNullOrWhiteSpace(adapterFamily))
                {
                    continue;
                }

                devices.Add(new UsbSerialDeviceInfo
                {
                    Name = name,
                    AdapterFamily = adapterFamily,
                    PnpDeviceId = pnpDeviceId,
                    Manufacturer = manufacturer,
                    Service = service,
                    Status = ReadManagementString(device, "Status"),
                    ConfigManagerErrorCode = ReadNullableInt(device, "ConfigManagerErrorCode"),
                    PortName = ExtractComPort(name)
                });
            }
        }
        catch (ManagementException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }

        return devices
            .GroupBy(device => device.PnpDeviceId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(device => device.PortName)
            .ThenBy(device => device.Name)
            .ToArray();
    }

    private static string IdentifyAdapterFamily(string name, string pnpDeviceId, string manufacturer, string service)
    {
        foreach (var adapter in KnownAdapters)
        {
            if (pnpDeviceId.Contains(adapter.PnpIdFragment, StringComparison.OrdinalIgnoreCase) ||
                name.Contains(adapter.NameFragment, StringComparison.OrdinalIgnoreCase) ||
                manufacturer.Contains(adapter.ManufacturerFragment, StringComparison.OrdinalIgnoreCase))
            {
                return adapter.Family;
            }
        }

        if (pnpDeviceId.StartsWith("BTHENUM", StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("(COM", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("serial", StringComparison.OrdinalIgnoreCase) ||
                service.Contains("BTHMODEM", StringComparison.OrdinalIgnoreCase)))
        {
            return "Bluetooth serial";
        }

        if (name.Contains("ELM", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("OBD", StringComparison.OrdinalIgnoreCase))
        {
            return "ELM/OBD serial adapter";
        }

        return "";
    }

    private static string ExtractComPort(string name)
    {
        var match = ComPortRegex().Match(name);
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string ReadManagementString(ManagementObject device, string propertyName)
    {
        return device.Properties[propertyName]?.Value?.ToString()?.Trim() ?? "";
    }

    private static int? ReadNullableInt(ManagementObject device, string propertyName)
    {
        var value = device.Properties[propertyName]?.Value;
        return value is null ? null : Convert.ToInt32(value);
    }

    [GeneratedRegex(@"\((COM\d+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex ComPortRegex();

    private sealed record KnownSerialAdapter(string Family, string PnpIdFragment, string NameFragment, string ManufacturerFragment);
}
