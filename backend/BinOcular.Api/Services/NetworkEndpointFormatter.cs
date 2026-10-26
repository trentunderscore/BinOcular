using System.Text.Json;
using BinOcular.Api.Models;

namespace BinOcular.Api.Services;

public sealed class NetworkEndpointFormatter : IToolResultFormatter
{
    public string ToolName => "Network Endpoint & C2 IOC Extractor";

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
                    ? "Network IOC extraction unavailable."
                    : result.StandardError
            });
            return output;
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;

            var total = root.TryGetProperty("totalEndpoints", out var te) ? te.GetInt32() : 0;
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";

            output.Sections.Add(new OutputSection
            {
                Title = "Network Analysis Summary",
                Type = "keyValue",
                Fields =
                [
                    new OutputField { Label = "Discovered Network IOCs", Value = total > 0 ? $"{total} Endpoints Identified" : "None Detected" },
                    new OutputField { Label = "Summary", Value = summary }
                ]
            });

            if (root.TryGetProperty("endpoints", out var epList) && epList.ValueKind == JsonValueKind.Array)
            {
                var rows = new List<List<string>>();

                foreach (var ep in epList.EnumerateArray())
                {
                    var type = ep.TryGetProperty("Type", out var t) ? t.GetString() ?? "" : "";
                    var val = ep.TryGetProperty("Value", out var v) ? v.GetString() ?? "" : "";
                    var context = ep.TryGetProperty("Context", out var c) ? c.GetString() ?? "" : "";
                    var risk = ep.TryGetProperty("Risk", out var r) ? r.GetString() ?? "" : "";

                    rows.Add([type, val, context, risk]);
                }

                if (rows.Count > 0)
                {
                    output.Sections.Add(new OutputSection
                    {
                        Title = "Identified Network Artifacts & Threat Indicators",
                        Type = "table",
                        Columns = ["Type", "Value / Target", "Context / Usage", "Risk"],
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
