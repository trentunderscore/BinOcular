using System.Text.Json;
using BinOcular.Api.Models;

namespace BinOcular.Api.Services;

public sealed class BinarySectionsFormatter : IToolResultFormatter
{
    public string ToolName => "Binary Sections & Entropy Visualizer";

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
                    ? "No section information extracted."
                    : result.StandardError
            });
            return output;
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;

            // Summary
            var summaryFields = new List<OutputField>();

            if (root.TryGetProperty("format", out var fmt))
            {
                summaryFields.Add(new OutputField { Label = "Image Structure", Value = fmt.GetString() ?? "Unknown" });
            }

            if (root.TryGetProperty("totalSections", out var total))
            {
                summaryFields.Add(new OutputField { Label = "Total Sections", Value = total.GetInt32().ToString() });
            }

            output.Sections.Add(new OutputSection
            {
                Title = "Image Section Header Overview",
                Type = "keyValue",
                Fields = summaryFields
            });

            // Suspicious Findings
            if (root.TryGetProperty("suspiciousFindings", out var findings) &&
                findings.ValueKind == JsonValueKind.Array &&
                findings.GetArrayLength() > 0)
            {
                var findingFields = new List<OutputField>();
                foreach (var f in findings.EnumerateArray())
                {
                    findingFields.Add(new OutputField
                    {
                        Label = "Anomaly Flag",
                        Value = f.GetString() ?? string.Empty
                    });
                }

                output.Sections.Add(new OutputSection
                {
                    Title = "Suspicious Section Anomalies",
                    Type = "tags",
                    Fields = findingFields
                });
            }

            // Table of Sections
            if (root.TryGetProperty("sections", out var sectionsList) && sectionsList.ValueKind == JsonValueKind.Array)
            {
                var rows = new List<List<string>>();

                foreach (var sec in sectionsList.EnumerateArray())
                {
                    var name = sec.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                    var vAddr = sec.TryGetProperty("VirtualAddress", out var va) ? va.GetString() ?? "" : "";
                    var vSize = sec.TryGetProperty("VirtualSize", out var vs) ? vs.GetString() ?? "" : "";
                    var rSize = sec.TryGetProperty("RawSize", out var rs) ? rs.GetString() ?? "" : "";
                    var perm = sec.TryGetProperty("Permissions", out var p) ? p.GetString() ?? "" : "";
                    var ent = sec.TryGetProperty("Entropy", out var e) ? e.GetDouble().ToString("F4") : "0.0000";
                    var status = sec.TryGetProperty("Status", out var st) ? st.GetString() ?? "Normal" : "Normal";

                    rows.Add([name, vAddr, vSize, rSize, perm, ent, status]);
                }

                output.Sections.Add(new OutputSection
                {
                    Title = "Section Headers & Entropy Breakdown",
                    Type = "table",
                    Columns = ["Section", "Virtual Addr", "Virtual Size", "Raw Size", "Perms", "Entropy", "Status"],
                    Rows = rows
                });
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
