using System.Text.Json;
using BinOcular.Api.Models;

namespace BinOcular.Api.Services;

public sealed class HeuristicRiskScorerFormatter : IToolResultFormatter
{
    public string ToolName => "Heuristic Threat & Anomaly Scorer";

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
                    ? "Heuristic risk scoring unavailable."
                    : result.StandardError
            });
            return output;
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;

            var score = root.TryGetProperty("threatScore", out var s) ? s.GetInt32() : 0;
            var verdict = root.TryGetProperty("verdict", out var v) ? v.GetString() ?? "" : "";
            var riskLevel = root.TryGetProperty("riskLevel", out var r) ? r.GetString() ?? "" : "";
            var total = root.TryGetProperty("totalFactors", out var tf) ? tf.GetInt32() : 0;

            output.Sections.Add(new OutputSection
            {
                Title = "Threat Assessment & Verdict",
                Type = "keyValue",
                Fields =
                [
                    new OutputField { Label = "Composite Threat Score", Value = $"{score} / 100" },
                    new OutputField { Label = "Risk Classification", Value = riskLevel },
                    new OutputField { Label = "Verdict", Value = verdict },
                    new OutputField { Label = "Total Triggered Indicators", Value = total.ToString() }
                ]
            });

            if (root.TryGetProperty("riskFactors", out var factorList) && factorList.ValueKind == JsonValueKind.Array)
            {
                var rows = new List<List<string>>();

                foreach (var factor in factorList.EnumerateArray())
                {
                    var fName = factor.TryGetProperty("Factor", out var fn) ? fn.GetString() ?? "" : "";
                    var fImpact = factor.TryGetProperty("Impact", out var fi) ? fi.GetString() ?? "" : "";
                    var fFinding = factor.TryGetProperty("Finding", out var ff) ? ff.GetString() ?? "" : "";
                    var fSev = factor.TryGetProperty("Severity", out var fs) ? fs.GetString() ?? "" : "";

                    rows.Add([fName, fImpact, fFinding, fSev]);
                }

                if (rows.Count > 0)
                {
                    output.Sections.Add(new OutputSection
                    {
                        Title = "Triggered Anomaly & Threat Factors",
                        Type = "table",
                        Columns = ["Risk Factor", "Score Impact", "Finding / Context", "Severity"],
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
