using TacomaDiag.Models;

namespace TacomaDiag.Services;

public static class ManufacturerModuleCatalog
{
    private static readonly IReadOnlyList<VehicleModuleTarget> StandardCanTargets =
    [
        Target("PCM / ECM", "Powertrain", "7E0", "7E8", "Primary engine/powertrain controller.", true),
        Target("TCM / transmission controller", "Transmission", "7E1", "7E9", "Primary transmission-controller candidate.", true),
        Target("TCM alternate", "Transmission", "7E2", "7EA", "Alternate transmission-controller candidate used by some layouts.", true),
        Target("ABS / stability control", "Chassis", "7E3", "7EB", "Brake/ABS/ESC candidate; support varies.", false),
        Target("SRS / airbag", "Safety", "7E4", "7EC", "Read-only candidate only; do not clear or code without proper tooling.", false),
        Target("BCM / body controller", "Body", "7E5", "7ED", "Body-controller candidate; support varies.", false),
        Target("Cluster / gateway", "Body", "7E6", "7EE", "Instrument cluster or gateway candidate; support varies.", false),
        Target("HVAC / comfort", "Body", "7E7", "7EF", "Comfort-module candidate; support varies.", false)
    ];

    private static readonly IReadOnlyList<VehicleModuleTarget> HyundaiKiaTargets =
    [
        Target("ECM / PCM", "Powertrain", "7E0", "7E8", "Hyundai/Kia engine-controller candidate.", true),
        Target("TCM / automatic transaxle", "Transmission", "7E1", "7E9", "Hyundai/Kia transmission-controller candidate.", true),
        Target("TCM alternate", "Transmission", "7E2", "7EA", "Alternate Hyundai/Kia transmission candidate.", true),
        Target("ABS / ESC", "Chassis", "7E3", "7EB", "ABS/ESC candidate; response support varies.", false),
        Target("SRS / airbag", "Safety", "7E4", "7EC", "Read-only airbag candidate; no clearing/coding.", false),
        Target("BCM", "Body", "7E5", "7ED", "Body-control candidate.", false),
        Target("Cluster", "Body", "7E6", "7EE", "Cluster candidate.", false),
        Target("EPS / steering", "Chassis", "7E7", "7EF", "Electric power steering candidate.", false)
    ];

    private static readonly IReadOnlyList<VehicleModuleTarget> ChryslerTargets =
    [
        Target("PCM / ECM", "Powertrain", "7E0", "7E8", "Primary engine controller; generic OBD-II should respond here.", true),
        Target("TCM / CVT controller", "Transmission", "7E1", "7E9", "First transmission-controller candidate for read-only DTC/ID probing.", true),
        Target("TCM alternate", "Transmission", "7E2", "7EA", "Alternate transmission-controller candidate used by some Chrysler/FCA layouts.", true),
        Target("ABS / ESP", "Chassis", "7E3", "7EB", "Chassis module candidate; enhanced support varies by adapter/module.", false),
        Target("ORC / Airbag", "Safety", "7E4", "7EC", "Read-only candidate only; do not attempt clearing or coding without proper tooling.", false),
        Target("BCM / TIPM", "Body", "7E5", "7ED", "Body/TIPM candidate; many functions need Chrysler-enhanced addressing.", false),
        Target("Cluster / CCN", "Body", "7E6", "7EE", "Cluster candidate; response support varies.", false),
        Target("HVAC / Occupant comfort", "Body", "7E7", "7EF", "Comfort-module candidate; response support varies.", false)
    ];

    private static readonly IReadOnlyList<VehicleModuleTarget> FordTargets =
    [
        Target("PCM", "Powertrain", "7E0", "7E8", "Ford powertrain controller candidate.", true),
        Target("TCM", "Transmission", "7E1", "7E9", "Ford transmission controller candidate.", true),
        Target("ABS", "Chassis", "760", "768", "Ford ABS candidate on some CAN networks; support varies.", false),
        Target("RCM / airbag", "Safety", "737", "73F", "Ford restraint control candidate; read-only only.", false),
        Target("BCM", "Body", "726", "72E", "Ford body-control candidate; support varies.", false)
    ];

    private static readonly IReadOnlyList<VehicleModuleTarget> GeneralMotorsTargets =
    [
        Target("ECM", "Powertrain", "7E0", "7E8", "GM engine controller candidate.", true),
        Target("TCM", "Transmission", "7E1", "7E9", "GM transmission controller candidate.", true),
        Target("EBCM / ABS", "Chassis", "241", "649", "GM brake-control candidate; support varies.", false),
        Target("SDM / airbag", "Safety", "257", "657", "GM sensing/diagnostic module candidate; read-only only.", false),
        Target("BCM", "Body", "243", "643", "GM body-control candidate; support varies.", false)
    ];

    private static readonly IReadOnlyList<VehicleModuleTarget> VolkswagenAudiTargets =
    [
        Target("Engine", "Powertrain", "7E0", "7E8", "VW/Audi engine candidate.", true),
        Target("Transmission", "Transmission", "7E1", "7E9", "VW/Audi transmission candidate.", true),
        Target("ABS brakes", "Chassis", "715", "71D", "VW/Audi ABS candidate; support varies.", false),
        Target("Airbag", "Safety", "715", "71D", "Airbag addressing varies heavily; read-only only.", false),
        Target("Gateway", "Body", "710", "718", "Gateway candidate; support varies.", false)
    ];

    public static IReadOnlyList<VehicleModuleTarget> GetTargets(VehicleProfile profile)
    {
        return profile.ManufacturerFamily switch
        {
            "Hyundai/Kia" => HyundaiKiaTargets,
            "Chrysler" => ChryslerTargets,
            "Ford" => Merge(FordTargets, StandardCanTargets),
            "General Motors" => Merge(GeneralMotorsTargets, StandardCanTargets),
            "VW/Audi" => Merge(VolkswagenAudiTargets, StandardCanTargets),
            "Toyota" or "Honda" or "Nissan" or "BMW" or "Mercedes" or "Subaru" or "Mazda" or "Volvo" or "Generic" => StandardCanTargets,
            _ => StandardCanTargets
        };
    }

    private static IReadOnlyList<VehicleModuleTarget> Merge(IReadOnlyList<VehicleModuleTarget> primary, IReadOnlyList<VehicleModuleTarget> fallback)
    {
        return primary
            .Concat(fallback)
            .GroupBy(target => $"{target.RequestHeader}:{target.Name}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static VehicleModuleTarget Target(string name, string system, string requestHeader, string responseHeader, string notes, bool isPriority)
    {
        return new VehicleModuleTarget
        {
            Name = name,
            System = system,
            RequestHeader = requestHeader,
            ResponseHeader = responseHeader,
            Notes = notes,
            IsPriority = isPriority
        };
    }
}
