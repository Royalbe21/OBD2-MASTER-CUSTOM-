namespace TacomaDiag.Models;

public sealed class DiagnosticEvent
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Category { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Details { get; init; } = "";
}

public sealed class DiagnosticSession
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTime StartedAt { get; init; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public string VehicleName { get; set; } = "";
    public string Vin { get; set; } = "";
    public string Connection { get; set; } = "";
    public string Protocol { get; set; } = "";
    public int StoredCodeCount { get; set; }
    public int PendingCodeCount { get; set; }
    public int PermanentCodeCount { get; set; }
    public int NotReadyMonitorCount { get; set; }
    public string HealthSummary { get; set; } = "";
    public List<DiagnosticEvent> Events { get; init; } = [];
}

public sealed class VehicleProfileRecord
{
    public string Name { get; init; } = "";
    public string Vin { get; init; } = "";
    public string Notes { get; init; } = "";
    public DateTime LastSeenAt { get; init; } = DateTime.Now;
}

public sealed class DiagnosticHistory
{
    public List<VehicleProfileRecord> Vehicles { get; init; } = [];
    public List<DiagnosticSession> Sessions { get; init; } = [];
}
