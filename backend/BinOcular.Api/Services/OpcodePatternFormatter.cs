using System.Text.Json;
using BinOcular.Api.Models;

namespace BinOcular.Api.Services;

public sealed class OpcodePatternFormatter : IToolResultFormatter
{
    public string ToolName => "Assembly & Shellcode Pattern Detector";

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
                    ? "Assembly pattern scanning unavailable."
                    : result.StandardError
            });
            return output;
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;

            var total = root.TryGetProperty("totalFindings", out var tf) ? tf.GetInt32() : 0;
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";

            output.Sections.Add(new OutputSection
            {
                Title = "Assembly & Opcode Assessment",
                Type = "keyValue",
                Fields =
                [
                    new OutputField { Label = "Identified Opcode Indicators", Value = total > 0 ? $"{total} Indicators Detected" : "None Detected" },
                    new OutputField { Label = "Assessment Summary", Value = summary }
                ]
            });

            if (root.TryGetProperty("findings", out var findingList) && findingList.ValueKind == JsonValueKind.Array)
            {
                var rows = new List<List<string>>();

                foreach (var f in findingList.EnumerateArray())
                {
                    var pattern = f.TryGetProperty("Pattern", out var p) ? p.GetString() ?? "" : "";
                    var category = f.TryGetProperty("Category", out var c) ? c.GetString() ?? "" : "";
                    var offset = f.TryGetProperty("Offset", out var o) ? o.GetString() ?? "" : "";
                    var details = f.TryGetProperty("Details", out var d) ? d.GetString() ?? "" : "";
                    var risk = f.TryGetProperty("Risk", out var r) ? r.GetString() ?? "" : "";

                    rows.Add([pattern, category, offset, details, risk]);
                }

                if (rows.Count > 0)
                {
                    output.Sections.Add(new OutputSection
                    {
                        Title = "Detected Opcodes, Shellcode & Evasion Stubs",
                        Type = "table",
                        Columns = ["Pattern", "Category", "Offset", "Details", "Risk"],
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
