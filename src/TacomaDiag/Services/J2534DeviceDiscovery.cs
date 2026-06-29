using Microsoft.Win32;

namespace TacomaDiag.Services;

public static class J2534DeviceDiscovery
{
    private static readonly string[] RegistryRoots =
    [
        @"SOFTWARE\PassThruSupport.04.04",
        @"SOFTWARE\PassThruSupport.05.00"
    ];

    public static IReadOnlyList<J2534DeviceInfo> FindInstalledDevices()
    {
        var devices = new List<J2534DeviceInfo>();
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            foreach (var root in RegistryRoots)
            {
                using var rootKey = baseKey.OpenSubKey(root);
                if (rootKey is null)
                {
                    continue;
                }

                foreach (var subKeyName in rootKey.GetSubKeyNames())
                {
                    using var deviceKey = rootKey.OpenSubKey(subKeyName);
                    if (deviceKey is null)
                    {
                        continue;
                    }

                    var library = ReadString(deviceKey, "FunctionLibrary");
                    if (string.IsNullOrWhiteSpace(library))
                    {
                        continue;
                    }

                    var name = ReadString(deviceKey, "Name");
                    var vendor = ReadString(deviceKey, "Vendor");
                    devices.Add(new J2534DeviceInfo
                    {
                        Name = string.IsNullOrWhiteSpace(name) ? subKeyName : name,
                        FunctionLibrary = Environment.ExpandEnvironmentVariables(library),
                        Vendor = string.IsNullOrWhiteSpace(vendor) ? subKeyName : vendor,
                        Source = $@"HKLM\{root}\{subKeyName} ({view})"
                    });
                }
            }
        }

        return devices
            .GroupBy(device => device.FunctionLibrary, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(device => device.Name)
            .ToArray();
    }

    private static string ReadString(RegistryKey key, string valueName)
    {
        return key.GetValue(valueName)?.ToString()?.Trim() ?? "";
    }
}
