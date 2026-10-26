using System.Text.Json;
using BinOcular.Api.Models;

namespace BinOcular.Api.Services;

public sealed class StringsFormatter : IToolResultFormatter
{
    public string ToolName => "String Analysis & IOC Extraction";

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
                    ? "No strings extracted."
                    : result.StandardError
            });
            return output;
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;

            // Section 1: Summary Key-Values
            var summaryFields = new List<OutputField>();

            if (root.TryGetProperty("totalExtractedCount", out var total))
            {
                summaryFields.Add(new OutputField
                {
                    Label = "Total Extracted Strings",
                    Value = total.GetInt32().ToString("N0")
                });
            }

            if (root.TryGetProperty("suspiciousIocCount", out var iocs))
            {
                var iocCount = iocs.GetInt32();
                summaryFields.Add(new OutputField
                {
                    Label = "Suspicious IOCs / APIs",
                    Value = iocCount > 0 ? $"{iocCount} (Flagged)" : "0 (None Detected)"
                });
            }

            summaryFields.Add(new OutputField
            {
                Label = "Full String Explorer",
                Value = "Available via Strings Explorer tab (interactive search, filters & export)"
            });

            output.Sections.Add(new OutputSection
            {
                Title = "String Extraction Metrics",
                Type = "keyValue",
                Fields = summaryFields
            });

            // Section 2: Suspicious IOC Table
            if (root.TryGetProperty("iocs", out var iocList) && iocList.ValueKind == JsonValueKind.Array)
            {
                var iocRows = new List<List<string>>();

                foreach (var ioc in iocList.EnumerateArray())
                {
                    var cat = ioc.TryGetProperty("Category", out var c) ? c.GetString() ?? "" : "";
                    var val = ioc.TryGetProperty("Value", out var v) ? v.GetString() ?? "" : "";
                    var sev = ioc.TryGetProperty("Severity", out var s) ? s.GetString() ?? "" : "";

                    iocRows.Add([cat, val, sev]);
                }

                if (iocRows.Count > 0)
                {
                    output.Sections.Add(new OutputSection
                    {
                        Title = "Detected Suspicious Indicators (IOCs)",
                        Type = "table",
                        Columns = ["Category", "Indicator / String", "Severity"],
                        Rows = iocRows
                    });
                }
            }

            // Section 3: Extracted Strings Table
            if (root.TryGetProperty("allStrings", out var allStrings) && allStrings.ValueKind == JsonValueKind.Array)
            {
                var stringRows = new List<List<string>>();
                var count = 0;
                foreach (var s in allStrings.EnumerateArray())
                {
                    if (count++ >= 150) break; // Keep initial card payload snappy
                    var off = s.TryGetProperty("offsetHex", out var oh) ? oh.GetString() ?? "" : "";
                    var enc = s.TryGetProperty("encoding", out var e) ? e.GetString() ?? "" : "";
                    var cat = s.TryGetProperty("category", out var c) ? c.GetString() ?? "Normal" : "Normal";
                    var val = s.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";

                    stringRows.Add([off, enc, cat, val]);
                }

                if (stringRows.Count > 0)
                {
                    output.Sections.Add(new OutputSection
                    {
                        Title = "Extracted Strings Preview (Top Entries)",
                        Type = "table",
                        Columns = ["Offset", "Encoding", "Type", "String Value"],
                        Rows = stringRows
                    });
                }
            }
            else if (root.TryGetProperty("sampleStrings", out var samples) && samples.ValueKind == JsonValueKind.Array)
            {
                var sampleList = new List<string>();
                foreach (var s in samples.EnumerateArray())
                {
                    var str = s.GetString();
                    if (!string.IsNullOrWhiteSpace(str)) sampleList.Add(str);
                }

                if (sampleList.Count > 0)
                {
                    output.Sections.Add(new OutputSection
                    {
                        Title = "Sample Extracted Strings Preview",
                        Type = "text",
                        Text = string.Join("\n", sampleList)
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
