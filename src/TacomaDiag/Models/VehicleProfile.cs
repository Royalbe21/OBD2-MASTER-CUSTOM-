namespace TacomaDiag.Models;

public sealed class VehicleProfile
{
    public string Name { get; init; } = "";
    public string Year { get; init; } = "";
    public string Make { get; init; } = "";
    public string Model { get; init; } = "";
    public string Engine { get; init; } = "";
    public string ExpectedProtocol { get; init; } = "";
    public string Notes { get; init; } = "";
    public string ManufacturerFamily { get; init; } = "";

    public static IReadOnlyList<VehicleProfile> BuiltInProfiles =>
    [
        ToyotaTacoma2008Base2TrFe,
        JeepCompass2016FwdFourCylinder,
        HyundaiElantra2014TwoLiter,
        DodgeSprinterDieselVan,
        RamProMasterEcoDieselVan,
        ChevroletExpressGmcSavanaVan,
        GenericHyundaiKiaCan,
        GenericFordLincolnCan,
        GenericGeneralMotorsCan,
        GenericHondaAcuraCan,
        GenericNissanInfinitiCan,
        GenericToyotaLexusCan,
        GenericChryslerJeepDodgeRamCan,
        GenericVolkswagenAudiCan,
        GenericBmwMiniCan,
        GenericMercedesBenzCan,
        GenericSubaruCan,
        GenericMazdaCan,
        GenericVolvoCan,
        GenericObdCan
    ];

    public static VehicleProfile ToyotaTacoma2008Base2TrFe { get; } = new()
    {
        Name = "2008 Toyota Tacoma Base 2.7L",
        Year = "2008",
        Make = "Toyota",
        Model = "Tacoma Base",
        Engine = "2TR-FE 2.7L inline-four",
        ExpectedProtocol = "ISO 15765-4 CAN, usually 11-bit / 500 kbps",
        Notes = "Prototype is read-focused except for confirmed OBD-II code clearing. Enhanced Toyota module probing is limited by ELM327 capability.",
        ManufacturerFamily = "Toyota"
    };

    public static VehicleProfile JeepCompass2016FwdFourCylinder { get; } = new()
    {
        Name = "2016 Jeep Compass FWD 4-cylinder",
        Year = "2016",
        Make = "Jeep",
        Model = "Compass FWD",
        Engine = "2.0L or 2.4L inline-four",
        ExpectedProtocol = "ISO 15765-4 CAN, usually 11-bit / 500 kbps",
        Notes = "Chrysler/FCA enhanced module coverage depends on adapter support and module addressing. ELM327 can attempt read-only OBD/UDS scans; fuller coverage normally requires a capable J2534 interface and Chrysler-enhanced definitions.",
        ManufacturerFamily = "Chrysler"
    };

    public static VehicleProfile HyundaiElantra2014TwoLiter { get; } = new()
    {
        Name = "2014 Hyundai Elantra 2.0L",
        Year = "2014",
        Make = "Hyundai",
        Model = "Elantra",
        Engine = "Nu 2.0L GDI/MPI inline-four, market dependent",
        ExpectedProtocol = "ISO 15765-4 CAN, usually 11-bit / 500 kbps",
        Notes = "Hyundai/Kia enhanced module access varies by adapter and module. ELM327 can attempt read-only OBD/UDS scans; fuller module coverage normally needs a capable J2534 interface and Hyundai/Kia enhanced definitions.",
        ManufacturerFamily = "Hyundai/Kia"
    };

    public static VehicleProfile DodgeSprinterDieselVan { get; } = new()
    {
        Name = "Dodge/Freightliner Sprinter diesel van",
        Year = "2003-2009",
        Make = "Dodge/Freightliner",
        Model = "Sprinter",
        Engine = "2.7L I5 diesel or 3.0L V6 diesel, year dependent",
        ExpectedProtocol = "Early vans may use ISO 9141/K-line; later NCV3 vans usually use CAN. Try auto-detect first, then CAN 11/500 on 2007+ vans.",
        Notes = "Sprinter diagnostics are Mercedes-derived. Generic OBD-II can read emissions powertrain data, but ABS/SRS/TCM/DEF-style enhanced coverage normally needs a Sprinter/Mercedes-capable scanner or a capable J2534 interface with enhanced definitions.",
        ManufacturerFamily = "Sprinter"
    };

    public static VehicleProfile RamProMasterEcoDieselVan { get; } = new()
    {
        Name = "Ram ProMaster 3.0L EcoDiesel van",
        Year = "2014-2018",
        Make = "Ram",
        Model = "ProMaster",
        Engine = "3.0L EcoDiesel inline-four",
        ExpectedProtocol = "ISO 15765-4 CAN, usually 11-bit / 500 kbps",
        Notes = "ProMaster diesel diagnostics are FCA/Ram-derived and may include diesel aftertreatment, DPF, DEF/SCR, and transmission/automated-manual concerns. This app performs read-only scans; forced regens, resets, relearns, and coding need verified enhanced tooling.",
        ManufacturerFamily = "Ram ProMaster"
    };

    public static VehicleProfile ChevroletExpressGmcSavanaVan { get; } = new()
    {
        Name = "Chevrolet Express / GMC Savana van",
        Year = "1996+",
        Make = "Chevrolet/GMC",
        Model = "Express / Savana",
        Engine = "Gas or Duramax diesel, year dependent",
        ExpectedProtocol = "CAN on later vans; older vans may use SAE J1850 VPW/Class 2. Use auto-detect first.",
        Notes = "Express/Savana diagnostics are GM-derived. Generic OBD-II covers emissions powertrain data; ABS, airbag, BCM, HVAC, and diesel aftertreatment detail depend on year, module network, adapter capability, and GM-enhanced definitions.",
        ManufacturerFamily = "General Motors Van"
    };

    public static VehicleProfile GenericHyundaiKiaCan { get; } = Generic("Hyundai/Kia CAN vehicle", "Hyundai/Kia", "Hyundai/Kia");
    public static VehicleProfile GenericFordLincolnCan { get; } = Generic("Ford/Lincoln CAN vehicle", "Ford/Lincoln", "Ford");
    public static VehicleProfile GenericGeneralMotorsCan { get; } = Generic("GM/Chevrolet/GMC/Cadillac/Buick CAN vehicle", "GM", "General Motors");
    public static VehicleProfile GenericHondaAcuraCan { get; } = Generic("Honda/Acura CAN vehicle", "Honda/Acura", "Honda");
    public static VehicleProfile GenericNissanInfinitiCan { get; } = Generic("Nissan/Infiniti CAN vehicle", "Nissan/Infiniti", "Nissan");
    public static VehicleProfile GenericToyotaLexusCan { get; } = Generic("Toyota/Lexus CAN vehicle", "Toyota/Lexus", "Toyota");
    public static VehicleProfile GenericChryslerJeepDodgeRamCan { get; } = Generic("Chrysler/Jeep/Dodge/Ram CAN vehicle", "Chrysler/Jeep/Dodge/Ram", "Chrysler");
    public static VehicleProfile GenericVolkswagenAudiCan { get; } = Generic("Volkswagen/Audi CAN vehicle", "Volkswagen/Audi", "VW/Audi");
    public static VehicleProfile GenericBmwMiniCan { get; } = Generic("BMW/Mini CAN vehicle", "BMW/Mini", "BMW");
    public static VehicleProfile GenericMercedesBenzCan { get; } = Generic("Mercedes-Benz CAN vehicle", "Mercedes-Benz", "Mercedes");
    public static VehicleProfile GenericSubaruCan { get; } = Generic("Subaru CAN vehicle", "Subaru", "Subaru");
    public static VehicleProfile GenericMazdaCan { get; } = Generic("Mazda CAN vehicle", "Mazda", "Mazda");
    public static VehicleProfile GenericVolvoCan { get; } = Generic("Volvo CAN vehicle", "Volvo", "Volvo");
    public static VehicleProfile GenericObdCan { get; } = Generic("Generic OBD-II CAN vehicle", "Generic", "Generic");

    public override string ToString() => Name;

    private static VehicleProfile Generic(string name, string make, string family)
    {
        return new VehicleProfile
        {
            Name = name,
            Year = "Generic",
            Make = make,
            Model = "CAN OBD-II",
            Engine = "Unknown",
            ExpectedProtocol = "ISO 15765-4 CAN, usually 11-bit / 500 kbps",
            Notes = "Generic profile uses safe read-only OBD-II and UDS-style module probes. Manufacturer-specific service support depends on adapter quality and vehicle module implementation.",
            ManufacturerFamily = family
        };
    }
}
