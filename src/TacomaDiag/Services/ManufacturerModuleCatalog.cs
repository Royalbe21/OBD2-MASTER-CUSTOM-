using TacomaDiag.Models;

namespace TacomaDiag.Services;

public static class ManufacturerModuleCatalog
{
    public const string ScanAll = "all";
    public const string ScanToyotaEnhanced = "toyota-enhanced";
    public const string ScanChryslerTransmission = "chrysler-transmission";
    public const string ScanHyundaiKiaModules = "hyundai-kia-modules";
    public const string ScanFordMazdaHsCan = "ford-mazda-hs";
    public const string ScanFordMazdaMsCan = "ford-mazda-ms";

    private static readonly IReadOnlyList<VehicleModuleTarget> StandardCanTargets =
    [
        Target("PCM / ECM", "Powertrain", "7E0", "7E8", "Primary engine/powertrain controller.", true, dataIdentifiers: ["F190", "F187"]),
        Target("TCM / transmission controller", "Transmission", "7E1", "7E9", "Primary transmission-controller candidate.", true, dataIdentifiers: ["F190", "F187"]),
        Target("TCM alternate", "Transmission", "7E2", "7EA", "Alternate transmission-controller candidate used by some layouts.", true, dataIdentifiers: ["F190", "F187"]),
        Target("ABS / stability control", "Chassis", "7E3", "7EB", "Brake/ABS/ESC candidate; support varies.", false),
        Target("SRS / airbag", "Safety", "7E4", "7EC", "Read-only candidate only; do not clear or code without proper tooling.", false),
        Target("BCM / body controller", "Body", "7E5", "7ED", "Body-controller candidate; support varies.", false),
        Target("Cluster / gateway", "Body", "7E6", "7EE", "Instrument cluster or gateway candidate; support varies.", false),
        Target("HVAC / comfort", "Body", "7E7", "7EF", "Comfort-module candidate; support varies.", false)
    ];

    private static readonly IReadOnlyList<VehicleModuleTarget> ToyotaTargets =
    [
        Target("ECM / 2TR-FE engine", "Powertrain", "7E0", "7E8", "Toyota powertrain candidate. Generic live data is reliable; enhanced IDs vary by ECU generation.", true, dataIdentifiers: ["F190", "F187", "F18C"]),
        Target("ECT / automatic transmission", "Transmission", "7E1", "7E9", "Toyota transmission candidate where separate ECT/TCM is present.", true, dataIdentifiers: ["F190", "F187"]),
        Target("ABS / VSC / TRAC", "Chassis", "7E3", "7EB", "Toyota ABS/VSC candidate; read-only only.", false, dataIdentifiers: ["F190", "F187"]),
        Target("SRS airbag", "Safety", "7E4", "7EC", "Toyota SRS candidate; read-only only and do not clear safety faults from this prototype.", false, dataIdentifiers: ["F190", "F187"]),
        Target("Body ECU / integration relay", "Body", "7E5", "7ED", "Toyota body ECU candidate; customization and active tests require Techstream/J2534-capable tooling.", false, dataIdentifiers: ["F190", "F187"]),
        Target("Combination meter", "Body", "7E6", "7EE", "Toyota cluster/combination meter candidate.", false, dataIdentifiers: ["F190", "F187"]),
        Target("A/C amplifier", "Body", "7E7", "7EF", "Toyota HVAC/A/C amplifier candidate.", false, dataIdentifiers: ["F190", "F187"])
    ];

    private static readonly IReadOnlyList<VehicleModuleTarget> HyundaiKiaTargets =
    [
        Target("ECM / PCM", "Powertrain", "7E0", "7E8", "Hyundai/Kia engine-controller candidate.", true, dataIdentifiers: ["F190", "F187", "F18C"]),
        Target("TCM / automatic transaxle", "Transmission", "7E1", "7E9", "Hyundai/Kia transmission-controller candidate.", true, dataIdentifiers: ["F190", "F187"]),
        Target("TCM alternate", "Transmission", "7E2", "7EA", "Alternate Hyundai/Kia transmission candidate.", true, dataIdentifiers: ["F190", "F187"]),
        Target("ABS / ESC", "Chassis", "7E3", "7EB", "ABS/ESC candidate; response support varies.", false, dataIdentifiers: ["F190", "F187"]),
        Target("SRS / airbag", "Safety", "7E4", "7EC", "Read-only airbag candidate; no clearing/coding.", false, dataIdentifiers: ["F190", "F187"]),
        Target("BCM", "Body", "7E5", "7ED", "Body-control candidate.", false, dataIdentifiers: ["F190", "F187"]),
        Target("Cluster", "Body", "7E6", "7EE", "Cluster candidate.", false, dataIdentifiers: ["F190", "F187"]),
        Target("EPS / steering", "Chassis", "7E7", "7EF", "Electric power steering candidate.", false, dataIdentifiers: ["F190", "F187"]),
        Target("TPMS / receiver", "Body", "7D0", "7D8", "Hyundai/Kia TPMS or receiver candidate where equipped; support varies.", false, dataIdentifiers: ["F190", "F187"]),
        Target("Smart key / immobilizer", "Body", "7D1", "7D9", "Hyundai/Kia smart-key candidate; read identity only, no immobilizer functions.", false, dataIdentifiers: ["F190", "F187"])
    ];

    private static readonly IReadOnlyList<VehicleModuleTarget> ChryslerTargets =
    [
        Target("PCM / ECM", "Powertrain", "7E0", "7E8", "Primary engine controller; generic OBD-II should respond here.", true, dataIdentifiers: ["F190", "F187", "F18C"]),
        Target("TCM / CVT controller", "Transmission", "7E1", "7E9", "First transmission-controller candidate for read-only DTC/ID probing.", true, dataIdentifiers: ["F190", "F187", "F18C", "F195"]),
        Target("TCM alternate", "Transmission", "7E2", "7EA", "Alternate transmission-controller candidate used by some Chrysler/FCA layouts.", true, dataIdentifiers: ["F190", "F187", "F18C", "F195"]),
        Target("TCM gateway candidate", "Transmission", "742", "74A", "Chrysler/FCA transmission candidate on some gateway layouts; support varies.", true, dataIdentifiers: ["F190", "F187"]),
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
        Target("PCM / HS-CAN", "Powertrain", "7E0", "7E8", "Ford/Mazda powertrain controller candidate. Set adapter switch to HS-CAN.", true, "HS-CAN", "Ford/Mazda HS-CAN switch", ["F190", "F187", "F188"]),
        Target("TCM / HS-CAN", "Transmission", "7E1", "7E9", "Ford/Mazda transmission controller candidate. Set adapter switch to HS-CAN.", true, "HS-CAN", "Ford/Mazda HS-CAN switch", ["F190", "F187"]),
        Target("ABS / HS-CAN", "Chassis", "760", "768", "Ford/Mazda ABS candidate on high-speed CAN; support varies. Set adapter switch to HS-CAN.", false, "HS-CAN", "Ford/Mazda HS-CAN switch", ["F190", "F187"]),
        Target("RCM / airbag / HS-CAN", "Safety", "737", "73F", "Ford/Mazda restraint-control candidate; read-only only. Set adapter switch to HS-CAN.", false, "HS-CAN", "Ford/Mazda HS-CAN switch", ["F190", "F187"]),
        Target("PSCM / steering / HS-CAN", "Chassis", "730", "738", "Ford/Mazda power-steering control candidate. Set adapter switch to HS-CAN.", false, "HS-CAN", "Ford/Mazda HS-CAN switch", ["F190", "F187"]),
        Target("BCM / MS-CAN", "Body", "726", "72E", "Ford/Mazda body-control candidate; support varies. Set adapter switch to MS-CAN.", false, "MS-CAN", "Ford/Mazda MS-CAN switch", ["F190", "F187"]),
        Target("IPC / cluster / MS-CAN", "Body", "720", "728", "Ford/Mazda instrument-cluster candidate. Set adapter switch to MS-CAN.", false, "MS-CAN", "Ford/Mazda MS-CAN switch", ["F190", "F187"]),
        Target("HVAC / climate / MS-CAN", "Body", "733", "73B", "Ford/Mazda HVAC/climate candidate. Set adapter switch to MS-CAN.", false, "MS-CAN", "Ford/Mazda MS-CAN switch", ["F190", "F187"]),
        Target("DDM / driver door / MS-CAN", "Body", "740", "748", "Ford/Mazda driver-door module candidate. Set adapter switch to MS-CAN.", false, "MS-CAN", "Ford/Mazda MS-CAN switch", ["F190", "F187"]),
        Target("APIM / infotainment / MS-CAN", "Body", "7D0", "7D8", "Ford SYNC/APIM-style candidate where equipped. Set adapter switch to MS-CAN.", false, "MS-CAN", "Ford/Mazda MS-CAN switch", ["F190", "F187"]),
        Target("SCCM / steering column / MS-CAN", "Body", "724", "72C", "Ford/Mazda steering-column candidate. Set adapter switch to MS-CAN.", false, "MS-CAN", "Ford/Mazda MS-CAN switch", ["F190", "F187"])
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
            "Toyota" => ToyotaTargets,
            "Honda" or "Nissan" or "BMW" or "Mercedes" or "Subaru" or "Volvo" or "Generic" => StandardCanTargets,
            _ => StandardCanTargets
        };
    }

    public static IReadOnlyList<VehicleModuleTarget> GetTargetsForScan(VehicleProfile profile, string scanKind)
    {
        var targets = GetTargets(profile);
        return scanKind switch
        {
            ScanToyotaEnhanced => ToyotaTargets,
            ScanChryslerTransmission => ChryslerTargets.Where(target => target.System.Equals("Transmission", StringComparison.OrdinalIgnoreCase)).ToArray(),
            ScanHyundaiKiaModules => HyundaiKiaTargets,
            ScanFordMazdaHsCan => FordTargets.Where(target => target.Bus.Equals("HS-CAN", StringComparison.OrdinalIgnoreCase)).ToArray(),
            ScanFordMazdaMsCan => FordTargets.Where(target => target.Bus.Equals("MS-CAN", StringComparison.OrdinalIgnoreCase)).ToArray(),
            _ => targets
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

    private static VehicleModuleTarget Target(
        string name,
        string system,
        string requestHeader,
        string responseHeader,
        string notes,
        bool isPriority,
        string bus = "CAN",
        string requiredAdapterProfile = "",
        IReadOnlyList<string>? dataIdentifiers = null)
    {
        return new VehicleModuleTarget
        {
            Name = name,
            System = system,
            RequestHeader = requestHeader,
            ResponseHeader = responseHeader,
            Bus = bus,
            RequiredAdapterProfile = requiredAdapterProfile,
            Notes = notes,
            DataIdentifiers = dataIdentifiers ?? [],
            IsPriority = isPriority
        };
    }
}
