using TacomaDiag.Models;

namespace TacomaDiag.Services;

public static class ChryslerModuleCatalog
{
    public static IReadOnlyList<VehicleModuleTarget> JeepCompass2016Targets { get; } =
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

    public static IReadOnlyList<VehicleModuleTarget> GetTargets(VehicleProfile profile)
    {
        return profile.ManufacturerFamily.Equals("Chrysler", StringComparison.OrdinalIgnoreCase)
            ? JeepCompass2016Targets
            : Array.Empty<VehicleModuleTarget>();
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
