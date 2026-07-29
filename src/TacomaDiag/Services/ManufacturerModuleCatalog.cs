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

    private static readonly IReadOnlyList<VehicleModuleTarget> RamProMasterDieselTargets =
    [
        Target("ECM / diesel powertrain", "Powertrain", "7E0", "7E8", "Primary EcoDiesel engine controller; DPF/DEF/SCR data usually requires enhanced definitions.", true),
        Target("Transmission / automated manual", "Transmission", "7E1", "7E9", "Transmission-controller candidate for ProMaster diesel vans.", true),
        Target("Transmission alternate", "Transmission", "7E2", "7EA", "Alternate transmission candidate used by some FCA layouts.", true),
        Target("ABS / ESC", "Chassis", "7E3", "7EB", "ABS/ESC candidate; support varies.", false),
        Target("ORC / airbag", "Safety", "7E4", "7EC", "Read-only restraint candidate; do not clear or code here.", false),
        Target("BCM / body computer", "Body", "7E5", "7ED", "Body-controller candidate for locks, lighting, and network-gateway related faults.", false),
        Target("Cluster / gateway", "Body", "7E6", "7EE", "Instrument cluster or gateway candidate; support varies.", false),
        Target("HVAC / comfort", "Body", "7E7", "7EF", "Comfort-module candidate; support varies.", false)
    ];

    private static readonly IReadOnlyList<VehicleModuleTarget> SprinterDieselTargets =
    [
        Target("CDI / diesel engine", "Powertrain", "7E0", "7E8", "Mercedes-derived diesel engine controller candidate.", true),
        Target("EGS / transmission", "Transmission", "7E1", "7E9", "Sprinter transmission-controller candidate.", true),
        Target("Transmission alternate", "Transmission", "7E2", "7EA", "Alternate transmission candidate for later CAN layouts.", true),
        Target("ESP / ABS", "Chassis", "7E3", "7EB", "Sprinter stability/ABS candidate; enhanced support varies heavily.", false),
        Target("SRS / restraint", "Safety", "7E4", "7EC", "Read-only restraint candidate; Sprinter-capable tooling is preferred.", false),
        Target("SAM / body module", "Body", "7E5", "7ED", "Signal acquisition/body-module candidate; support varies.", false),
        Target("Instrument cluster", "Body", "7E6", "7EE", "Cluster candidate; support varies.", false),
        Target("HVAC / auxiliary heat", "Body", "7E7", "7EF", "Comfort or auxiliary-heat candidate; support varies.", false)
    ];

    private static readonly IReadOnlyList<VehicleModuleTarget> FordTargets =
    [
        Target("PCM / HS-CAN", "Powertrain", "7E0", "7E8", "Ford/Mazda powertrain controller candidate. Set adapter switch to HS-CAN.", true),
        Target("TCM / HS-CAN", "Transmission", "7E1", "7E9", "Ford/Mazda transmission controller candidate. Set adapter switch to HS-CAN.", true),
        Target("ABS / HS-CAN", "Chassis", "760", "768", "Ford/Mazda ABS candidate on high-speed CAN; support varies. Set adapter switch to HS-CAN.", false),
        Target("RCM / airbag / HS-CAN", "Safety", "737", "73F", "Ford/Mazda restraint-control candidate; read-only only. Set adapter switch to HS-CAN.", false),
        Target("BCM / MS-CAN", "Body", "726", "72E", "Ford/Mazda body-control candidate; support varies. Set adapter switch to MS-CAN.", false),
        Target("IPC / cluster / MS-CAN", "Body", "720", "728", "Ford/Mazda instrument-cluster candidate. Set adapter switch to MS-CAN.", false),
        Target("HVAC / climate / MS-CAN", "Body", "733", "73B", "Ford/Mazda HVAC/climate candidate. Set adapter switch to MS-CAN.", false),
        Target("DDM / driver door / MS-CAN", "Body", "740", "748", "Ford/Mazda driver-door module candidate. Set adapter switch to MS-CAN.", false),
        Target("APIM / infotainment / MS-CAN", "Body", "7D0", "7D8", "Ford SYNC/APIM-style candidate where equipped. Set adapter switch to MS-CAN.", false)
    ];

    private static readonly IReadOnlyList<VehicleModuleTarget> GeneralMotorsTargets =
    [
        Target("ECM", "Powertrain", "7E0", "7E8", "GM engine controller candidate.", true),
        Target("TCM", "Transmission", "7E1", "7E9", "GM transmission controller candidate.", true),
        Target("EBCM / ABS", "Chassis", "241", "649", "GM brake-control candidate; support varies.", false),
        Target("SDM / airbag", "Safety", "257", "657", "GM sensing/diagnostic module candidate; read-only only.", false),
        Target("BCM", "Body", "243", "643", "GM body-control candidate; support varies.", false)
    ];

    private static readonly IReadOnlyList<VehicleModuleTarget> GeneralMotorsVanTargets =
    [
        Target("ECM / engine", "Powertrain", "7E0", "7E8", "GM Express/Savana engine controller candidate.", true),
        Target("TCM / transmission", "Transmission", "7E1", "7E9", "GM Express/Savana transmission controller candidate.", true),
        Target("EBCM / ABS", "Chassis", "241", "649", "GM van brake-control candidate; support varies.", false),
        Target("SDM / airbag", "Safety", "257", "657", "GM van sensing/diagnostic module candidate; read-only only.", false),
        Target("BCM / body", "Body", "243", "643", "GM van body-control candidate; support varies.", false),
        Target("IPC / instrument cluster", "Body", "250", "650", "GM van instrument-cluster candidate; support varies.", false),
        Target("HVAC / climate", "Body", "248", "648", "GM van HVAC candidate; support varies.", false)
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
            "Ram ProMaster" => RamProMasterDieselTargets,
            "Sprinter" => SprinterDieselTargets,
            "Ford" or "Mazda" => Merge(FordTargets, StandardCanTargets),
            "General Motors" => Merge(GeneralMotorsTargets, StandardCanTargets),
            "General Motors Van" => Merge(GeneralMotorsVanTargets, StandardCanTargets),
            "VW/Audi" => Merge(VolkswagenAudiTargets, StandardCanTargets),
            "Toyota" or "Honda" or "Nissan" or "BMW" or "Mercedes" or "Subaru" or "Volvo" or "Generic" => StandardCanTargets,
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
