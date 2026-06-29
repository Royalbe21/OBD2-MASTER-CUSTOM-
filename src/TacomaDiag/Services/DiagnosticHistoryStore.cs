using System.Text.Json;
using System.IO;
using TacomaDiag.Models;

namespace TacomaDiag.Services;

public sealed class DiagnosticHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string AppDataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TacomaDiag");

    public string HistoryPath => Path.Combine(AppDataDirectory, "diagnostic-history.json");

    public DiagnosticHistory Load()
    {
        Directory.CreateDirectory(AppDataDirectory);
        if (!File.Exists(HistoryPath))
        {
            return new DiagnosticHistory();
        }

        var json = File.ReadAllText(HistoryPath);
        return JsonSerializer.Deserialize<DiagnosticHistory>(json, JsonOptions) ?? new DiagnosticHistory();
    }

    public void Save(DiagnosticHistory history)
    {
        Directory.CreateDirectory(AppDataDirectory);
        var json = JsonSerializer.Serialize(history, JsonOptions);
        File.WriteAllText(HistoryPath, json);
    }

    public void UpsertSession(DiagnosticSession session)
    {
        var history = Load();
        var existing = history.Sessions.FindIndex(item => item.Id == session.Id);
        if (existing >= 0)
        {
            history.Sessions[existing] = session;
        }
        else
        {
            history.Sessions.Insert(0, session);
        }

        if (!string.IsNullOrWhiteSpace(session.Vin))
        {
            history.Vehicles.RemoveAll(vehicle => vehicle.Vin.Equals(session.Vin, StringComparison.OrdinalIgnoreCase));
            history.Vehicles.Insert(0, new VehicleProfileRecord
            {
                Name = string.IsNullOrWhiteSpace(session.VehicleName) ? "Unknown vehicle" : session.VehicleName,
                Vin = session.Vin,
                Notes = session.Protocol,
                LastSeenAt = DateTime.Now
            });
        }

        while (history.Sessions.Count > 100)
        {
            history.Sessions.RemoveAt(history.Sessions.Count - 1);
        }

        while (history.Vehicles.Count > 25)
        {
            history.Vehicles.RemoveAt(history.Vehicles.Count - 1);
        }

        Save(history);
    }
}
