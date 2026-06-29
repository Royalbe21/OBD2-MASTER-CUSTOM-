namespace TacomaDiag.Models;

public sealed class MonitorStatus
{
    public string Name { get; init; } = "";
    public string Supported { get; init; } = "";
    public string Ready { get; init; } = "";
    public string Status { get; init; } = "";
}

public sealed class ReadinessSnapshot
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public bool MilOn { get; init; }
    public int ConfirmedDtcCount { get; init; }
    public string EngineType { get; init; } = "";
    public string RawResponse { get; init; } = "";
    public IReadOnlyList<MonitorStatus> Monitors { get; init; } = Array.Empty<MonitorStatus>();
}
