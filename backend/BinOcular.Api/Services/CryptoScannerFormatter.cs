using System.Text.Json;
using BinOcular.Api.Models;

namespace BinOcular.Api.Services;

public sealed class CryptoScannerFormatter : IToolResultFormatter
{
    public string ToolName => "Cryptographic Algorithm & Constant Detector";

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
                    ? "No cryptographic patterns detected."
                    : result.StandardError
            });
            return output;
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;

            var total = root.TryGetProperty("totalDetected", out var t) ? t.GetInt32() : 0;
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";

            output.Sections.Add(new OutputSection
            {
                Title = "Cryptography Scan Summary",
                Type = "keyValue",
                Fields =
                [
                    new OutputField { Label = "Identified Cryptographic Constants", Value = total > 0 ? $"{total} Signatures Found" : "None Detected" },
                    new OutputField { Label = "Assessment", Value = summary }
                ]
            });

            if (root.TryGetProperty("detections", out var detList) && detList.ValueKind == JsonValueKind.Array)
            {
                var rows = new List<List<string>>();

                foreach (var d in detList.EnumerateArray())
                {
                    var algo = d.TryGetProperty("Algorithm", out var a) ? a.GetString() ?? "" : "";
                    var pat = d.TryGetProperty("PatternType", out var p) ? p.GetString() ?? "" : "";
                    var off = d.TryGetProperty("Offset", out var o) ? o.GetString() ?? "" : "";
                    var conf = d.TryGetProperty("Confidence", out var c) ? c.GetString() ?? "" : "";

                    rows.Add([algo, pat, off, conf]);
                }

                if (rows.Count > 0)
                {
                    output.Sections.Add(new OutputSection
                    {
                        Title = "Detected Cryptographic Algorithms & S-Boxes",
                        Type = "table",
                        Columns = ["Algorithm", "Pattern Type", "Offset", "Confidence"],
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
