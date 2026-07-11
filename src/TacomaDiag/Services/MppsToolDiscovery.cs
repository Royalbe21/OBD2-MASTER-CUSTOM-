using Microsoft.Win32;
using System.IO;
using System.Management;
using TacomaDiag.Models;

namespace TacomaDiag.Services;

public static class MppsToolDiscovery
{
    private static readonly string[] RegistryUninstallRoots =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    ];

    private static readonly string[] CommonExecutableNames =
    [
        "mpps.exe",
        "mpps v16.exe",
        "mpps16.exe",
        "mpps v16.1.02.exe"
    ];

    public static IReadOnlyList<MppsToolInfo> FindInstalledTools()
    {
        var candidates = new List<MppsToolInfo>();
        candidates.AddRange(FindFromUninstallRegistry());
        candidates.AddRange(FindFromCommonFolders());

        return candidates
            .Where(candidate => File.Exists(candidate.ExecutablePath))
            .GroupBy(candidate => candidate.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.Name)
            .ToArray();
    }

    public static MppsToolInfo FromManualPath(string path)
    {
        return new MppsToolInfo
        {
            Name = $"Manual: {Path.GetFileName(path)}",
            ExecutablePath = path,
            Source = "Manual selection",
            Status = File.Exists(path) ? "Ready" : "Missing executable"
        };
    }

    public static IReadOnlyList<MppsUsbDeviceInfo> FindConnectedUsbDevices()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var devices = new List<MppsUsbDeviceInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\CIMV2",
                "SELECT * FROM Win32_PnPEntity WHERE PNPDeviceID LIKE 'USB%'");

            foreach (ManagementObject device in searcher.Get().Cast<ManagementObject>())
            {
                var name = ReadManagementString(device, "Name");
                var pnpDeviceId = ReadManagementString(device, "PNPDeviceID");
                if (!LooksLikeMppsDevice(name, pnpDeviceId))
                {
                    continue;
                }

                devices.Add(new MppsUsbDeviceInfo
                {
                    Name = name,
                    PnpDeviceId = pnpDeviceId,
                    Manufacturer = ReadManagementString(device, "Manufacturer"),
                    Service = ReadManagementString(device, "Service"),
                    Status = ReadManagementString(device, "Status"),
                    ConfigManagerErrorCode = ReadNullableInt(device, "ConfigManagerErrorCode"),
                    HardwareIds = ReadManagementStringArray(device, "HardwareID")
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
            .OrderBy(device => device.Name)
            .ToArray();
    }

    public static string BuildSafetyChecklist(
        MppsToolInfo? selectedTool,
        IReadOnlyList<MppsUsbDeviceInfo>? usbDevices = null,
        int j2534DeviceCount = 0)
    {
        var path = selectedTool?.ExecutablePath ?? "Not selected";
        var status = selectedTool is null ? "Not configured" : selectedTool.Status;
        var lines = new List<string>
        {
            "MPPS V16 Integration",
            "====================",
            $"Selected executable: {path}",
            $"Status: {status}",
            "",
            "Windows USB status:"
        };

        if (usbDevices is { Count: > 0 })
        {
            foreach (var device in usbDevices)
            {
                lines.Add($"- {device.Name}: {device.DriverSummary}");
                lines.Add($"  PNP ID: {device.PnpDeviceId}");
                if (device.HardwareIds.Count > 0)
                {
                    lines.Add($"  Hardware IDs: {string.Join(", ", device.HardwareIds)}");
                }
            }
        }
        else
        {
            lines.Add("- No MPPS USB device found by Windows PnP.");
        }

        lines.Add("");
        lines.Add($"J2534 PassThru DLLs registered on this PC: {j2534DeviceCount}");
        lines.Add("");
        lines.AddRange(
        [
            "TacomaDiag treats MPPS V16 as an external ECU flasher companion.",
            "MPPS V16 does not expose a known ELM327-style serial command set.",
            "Use TacomaDiag for scan/readiness/report work and launch MPPS only for MPPS-supported ECU read/write workflows.",
            "",
            "Before any MPPS read/write session:",
            "1. Use a stable battery support supply.",
            "2. Save a TacomaDiag before-work report.",
            "3. Read and save the original ECU file in MPPS before writing anything.",
            "4. Confirm the ECU family/driver selection in MPPS matches the vehicle.",
            "5. Do not write files intended to disable emissions or safety systems.",
            "6. Do not unplug USB, switch ignition, or let the laptop sleep during an MPPS operation.",
            "",
            "If your MPPS package installed a real J2534 PassThru DLL, use TacomaDiag's J2534 mode instead of this launcher."
        ]);

        return string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<MppsToolInfo> FindFromUninstallRegistry()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            foreach (var root in RegistryUninstallRoots)
            {
                using var uninstallKey = baseKey.OpenSubKey(root);
                if (uninstallKey is null)
                {
                    continue;
                }

                foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                {
                    using var appKey = uninstallKey.OpenSubKey(subKeyName);
                    if (appKey is null)
                    {
                        continue;
                    }

                    var displayName = ReadString(appKey, "DisplayName");
                    if (!displayName.Contains("MPPS", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var installLocation = Environment.ExpandEnvironmentVariables(ReadString(appKey, "InstallLocation"));
                    foreach (var executable in FindExecutablesInFolder(installLocation))
                    {
                        yield return new MppsToolInfo
                        {
                            Name = displayName,
                            ExecutablePath = executable,
                            Source = $@"HKLM\{root}\{subKeyName} ({view})",
                            Status = "Ready"
                        };
                    }
                }
            }
        }
    }

    private static IEnumerable<MppsToolInfo> FindFromCommonFolders()
    {
        foreach (var root in CommonRoots())
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(root, "*MPPS*", SearchOption.TopDirectoryOnly))
            {
                foreach (var executable in FindExecutablesInFolder(directory))
                {
                    yield return new MppsToolInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(executable),
                        ExecutablePath = executable,
                        Source = directory,
                        Status = "Ready"
                    };
                }
            }
        }
    }

    private static IEnumerable<string> CommonRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }

    private static IEnumerable<string> FindExecutablesInFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            yield break;
        }

        foreach (var executableName in CommonExecutableNames)
        {
            var directPath = Path.Combine(folder, executableName);
            if (File.Exists(directPath))
            {
                yield return directPath;
            }
        }

        foreach (var executable in Directory.EnumerateFiles(folder, "*.exe", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(executable);
            if (fileName.Contains("mpps", StringComparison.OrdinalIgnoreCase))
            {
                yield return executable;
            }
        }
    }

    private static string ReadString(RegistryKey key, string valueName)
    {
        return key.GetValue(valueName)?.ToString()?.Trim() ?? "";
    }

    private static bool LooksLikeMppsDevice(string name, string pnpDeviceId)
    {
        return pnpDeviceId.Contains(@"USB\VID_1C43&PID_0500", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Amt Flash", StringComparison.OrdinalIgnoreCase)
            || name.Contains("MPPS", StringComparison.OrdinalIgnoreCase);
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

    private static IReadOnlyList<string> ReadManagementStringArray(ManagementObject device, string propertyName)
    {
        return device.Properties[propertyName]?.Value is string[] values
            ? values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray()
            : [];
    }
}
