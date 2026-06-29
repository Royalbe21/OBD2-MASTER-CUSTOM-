using System.Globalization;
using System.Net;
using System.Text;
using TacomaDiag.Models;

namespace TacomaDiag.Services;

public static class ReportExportService
{
    public static string BuildHtmlReport(string plainTextReport, IEnumerable<HealthFinding> findings, IEnumerable<LiveRecordingFrame> recordings)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\">");
        builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine("<title>TacomaDiag Report</title>");
        builder.AppendLine("<style>");
        builder.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;background:#101418;color:#e8edf2;margin:0;padding:32px;}");
        builder.AppendLine("main{max-width:980px;margin:0 auto;} h1,h2{color:#7dd3c7;} pre,table{background:#171d23;border:1px solid #3b4652;border-radius:6px;} pre{padding:18px;white-space:pre-wrap;} table{width:100%;border-collapse:collapse;margin:12px 0 24px;} th,td{padding:8px 10px;border-bottom:1px solid #3b4652;text-align:left;} th{background:#202832;} .muted{color:#aab6c2;}");
        builder.AppendLine("</style>");
        builder.AppendLine("</head><body><main>");
        builder.AppendLine("<h1>TacomaDiag Report</h1>");
        builder.AppendLine($"<p class=\"muted\">Generated {WebUtility.HtmlEncode(DateTime.Now.ToString("G", CultureInfo.CurrentCulture))}</p>");
        builder.AppendLine("<h2>Summary</h2>");
        builder.AppendLine($"<pre>{WebUtility.HtmlEncode(plainTextReport)}</pre>");

        var findingList = findings.ToList();
        if (findingList.Count > 0)
        {
            builder.AppendLine("<h2>Advisor Findings</h2>");
            builder.AppendLine("<table><thead><tr><th>Severity</th><th>Area</th><th>Finding</th><th>Next Step</th></tr></thead><tbody>");
            foreach (var finding in findingList)
            {
                builder.AppendLine("<tr>" +
                    $"<td>{WebUtility.HtmlEncode(finding.Severity)}</td>" +
                    $"<td>{WebUtility.HtmlEncode(finding.Area)}</td>" +
                    $"<td>{WebUtility.HtmlEncode(finding.Finding)}</td>" +
                    $"<td>{WebUtility.HtmlEncode(finding.NextStep)}</td>" +
                    "</tr>");
            }

            builder.AppendLine("</tbody></table>");
        }

        var recordingList = recordings.ToList();
        if (recordingList.Count > 0)
        {
            builder.AppendLine("<h2>Live Recording Snapshot</h2>");
            builder.AppendLine("<table><thead><tr><th>Time</th><th>RPM</th><th>Speed</th><th>Coolant</th><th>STFT</th><th>LTFT</th><th>Voltage</th></tr></thead><tbody>");
            foreach (var frame in recordingList.TakeLast(25))
            {
                builder.AppendLine("<tr>" +
                    $"<td>{WebUtility.HtmlEncode(frame.Timestamp.ToString("T", CultureInfo.CurrentCulture))}</td>" +
                    $"<td>{WebUtility.HtmlEncode(frame.Rpm)}</td>" +
                    $"<td>{WebUtility.HtmlEncode(frame.Speed)}</td>" +
                    $"<td>{WebUtility.HtmlEncode(frame.Coolant)}</td>" +
                    $"<td>{WebUtility.HtmlEncode(frame.ShortTermFuelTrim)}</td>" +
                    $"<td>{WebUtility.HtmlEncode(frame.LongTermFuelTrim)}</td>" +
                    $"<td>{WebUtility.HtmlEncode(frame.Voltage)}</td>" +
                    "</tr>");
            }

            builder.AppendLine("</tbody></table>");
        }

        builder.AppendLine("</main></body></html>");
        return builder.ToString();
    }

    public static string BuildLiveRecordingCsv(IEnumerable<LiveRecordingFrame> frames)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Timestamp,RPM,Speed,Coolant,ShortTermFuelTrim,LongTermFuelTrim,Voltage");
        foreach (var frame in frames)
        {
            builder.AppendLine(string.Join(",",
            [
                Csv(frame.Timestamp.ToString("O", CultureInfo.InvariantCulture)),
                Csv(frame.Rpm),
                Csv(frame.Speed),
                Csv(frame.Coolant),
                Csv(frame.ShortTermFuelTrim),
                Csv(frame.LongTermFuelTrim),
                Csv(frame.Voltage)
            ]));
        }

        return builder.ToString();
    }

    private static string Csv(string value)
    {
        if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
