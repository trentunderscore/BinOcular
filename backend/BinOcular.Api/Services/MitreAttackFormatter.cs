using System.Text.Json;
using BinOcular.Api.Models;

namespace BinOcular.Api.Services;

public sealed class MitreAttackFormatter : IToolResultFormatter
{
    public string ToolName => "MITRE ATT&CK Framework Matrix Mapper";

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
                    ? "MITRE ATT&CK mapping unavailable."
                    : result.StandardError
            });
            return output;
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;

            var total = root.TryGetProperty("totalTechniques", out var tt) ? tt.GetInt32() : 0;
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";

            output.Sections.Add(new OutputSection
            {
                Title = "MITRE ATT&CK Mapping Overview",
                Type = "keyValue",
                Fields =
                [
                    new OutputField { Label = "Identified MITRE Techniques", Value = total > 0 ? $"{total} Techniques Flagged" : "None Detected" },
                    new OutputField { Label = "Tactical Assessment", Value = summary }
                ]
            });

            if (root.TryGetProperty("techniques", out var techList) && techList.ValueKind == JsonValueKind.Array)
            {
                var rows = new List<List<string>>();

                foreach (var tech in techList.EnumerateArray())
                {
                    var tactic = tech.TryGetProperty("Tactic", out var tac) ? tac.GetString() ?? "" : "";
                    var id = tech.TryGetProperty("TechniqueId", out var tid) ? tid.GetString() ?? "" : "";
                    var name = tech.TryGetProperty("TechniqueName", out var tn) ? tn.GetString() ?? "" : "";
                    var evidence = tech.TryGetProperty("MatchedEvidence", out var ev) ? ev.GetString() ?? "" : "";
                    var desc = tech.TryGetProperty("Description", out var d) ? d.GetString() ?? "" : "";

                    rows.Add([tactic, id, name, evidence, desc]);
                }

                if (rows.Count > 0)
                {
                    output.Sections.Add(new OutputSection
                    {
                        Title = "Tactical Matrix Mappings",
                        Type = "table",
                        Columns = ["Tactic", "Technique ID", "Technique Name", "Matched Evidence", "Description"],
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
