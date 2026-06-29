using System.IO;

namespace TacomaDiag.Services;

public sealed class J2534DeviceInfo
{
    public string Name { get; init; } = "";
    public string FunctionLibrary { get; init; } = "";
    public string Vendor { get; init; } = "";
    public string Source { get; init; } = "";

    public bool Exists => File.Exists(FunctionLibrary);

    public override string ToString()
    {
        var suffix = Exists ? "" : " (missing DLL)";
        return $"{Name}{suffix}";
    }

    public static J2534DeviceInfo FromManualPath(string path)
    {
        return new J2534DeviceInfo
        {
            Name = $"Manual: {Path.GetFileName(path)}",
            FunctionLibrary = path,
            Vendor = "Manual",
            Source = "Manual selection"
        };
    }
}
