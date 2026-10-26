using System.Text.Json;
using BinOcular.Api.Models;

namespace BinOcular.Api.Services;

public sealed class EmbeddedAssetsFormatter : IToolResultFormatter
{
    public string ToolName => "Embedded Files & Firmware Payload Scanner";

    public FormattedOutput Format(ToolResult result)
    {
        var output = new FormattedOutput
        {
            Title = ToolName
        };

        if (string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            output.Sections.Add(new OutputSection
            {
                Title = "Error",
                Type = "text",
                Text = string.IsNullOrWhiteSpace(result.StandardError)
                    ? "No embedded asset data produced."
                    : result.StandardError
            });
            return output;
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;

            var total = root.TryGetProperty("totalAssetsFound", out var t) ? t.GetInt32() : 0;
            var isDropper = root.TryGetProperty("hasDropperPayload", out var d) && d.GetBoolean();
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";

            output.Sections.Add(new OutputSection
            {
                Title = "Embedded Payload Triage",
                Type = "keyValue",
                Fields =
                [
                    new OutputField { Label = "Total Nested Assets", Value = total.ToString() },
                    new OutputField { Label = "Dropper Indicator", Value = isDropper ? "CRITICAL: Secondary Executable Embedded" : "None" },
                    new OutputField { Label = "Triage Assessment", Value = summary }
                ]
            });

            if (root.TryGetProperty("assets", out var assetList) && assetList.ValueKind == JsonValueKind.Array)
            {
                var rows = new List<List<string>>();

                foreach (var a in assetList.EnumerateArray())
                {
                    var type = a.TryGetProperty("Type", out var ty) ? ty.GetString() ?? "" : "";
                    var off = a.TryGetProperty("Offset", out var o) ? o.GetString() ?? "" : "";
                    var sev = a.TryGetProperty("Severity", out var sv) ? sv.GetString() ?? "" : "";
                    var desc = a.TryGetProperty("Description", out var ds) ? ds.GetString() ?? "" : "";

                    rows.Add([type, off, sev, desc]);
                }

                if (rows.Count > 0)
                {
                    output.Sections.Add(new OutputSection
                    {
                        Title = "Identified Nested Containers & Payloads",
                        Type = "table",
                        Columns = ["Payload Type", "File Offset", "Severity", "Description"],
                        Rows = rows
                    });
                }
            }
        }
        catch (JsonException)
        {
            output.Sections.Add(new OutputSection
            {
                Title = "Output",
                Type = "text",
                Text = result.StandardOutput
            });
        }

        return output;
    }
}
