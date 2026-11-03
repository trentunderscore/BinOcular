using System.Text.Json;
using BinOcular.Api.Models;

namespace BinOcular.Api.Services;

public sealed class DIEToolResultFormatter : IToolResultFormatter
{
    public string ToolName => "Detect It Easy";

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
                Title = "Output",
                Type = "text",
                Text = string.IsNullOrWhiteSpace(result.StandardError)
                    ? "No output was produced."
                    : result.StandardError
            });

            return output;
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;

            var summaryFields = new List<OutputField>();

            if (root.TryGetProperty("filetype", out var fileType))
            {
                summaryFields.Add(new OutputField
                {
                    Label = "File Type",
                    Value = fileType.GetString() ?? "Unknown"
                });
            }

            if (root.TryGetProperty("name", out var fileName))
            {
                summaryFields.Add(new OutputField
                {
                    Label = "Target Name",
                    Value = fileName.GetString() ?? "Unknown"
                });
            }

            if (root.TryGetProperty("size", out var size))
            {
                summaryFields.Add(new OutputField
                {
                    Label = "Size (Bytes)",
                    Value = size.ToString()
                });
            }

            if (root.TryGetProperty("entropy", out var entropy))
            {
                summaryFields.Add(new OutputField
                {
                    Label = "Entropy",
                    Value = entropy.ToString()
                });
            }

            if (root.TryGetProperty("status", out var status))
            {
                summaryFields.Add(new OutputField
                {
                    Label = "Packing Status",
                    Value = status.GetString() ?? "Unknown"
                });
            }

            if (root.TryGetProperty("total", out var total))
            {
                summaryFields.Add(new OutputField
                {
                    Label = "Total Entropy",
                    Value = total.ToString()
                });
            }

            if (summaryFields.Count > 0)
            {
                output.Sections.Add(new OutputSection
                {
                    Title = "File Summary",
                    Type = "keyValue",
                    Fields = summaryFields
                });
            }

            // Modern DIE 3.x detects array
            if (root.TryGetProperty("detects", out var detects) && detects.ValueKind == JsonValueKind.Array)
            {
                var detectionSection = new OutputSection
                {
                    Title = "Detections",
                    Type = "table",
                    Columns = ["Category", "Name", "Version", "Details"]
                };

                foreach (var detectGroup in detects.EnumerateArray())
                {
                    var category = GetString(detectGroup, "type");
                    if (detectGroup.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var val in values.EnumerateArray())
                        {
                            var name = GetString(val, "name");
                            var version = GetString(val, "version");
                            var details = val.TryGetProperty("string", out var strVal) ? strVal.GetString() ?? "" :
                                          val.TryGetProperty("info", out var infoVal) ? infoVal.GetString() ?? "" : "";

                            detectionSection.Rows.Add([category, name, version, details]);
                        }
                    }
                }

                if (detectionSection.Rows.Count > 0)
                {
                    output.Sections.Add(detectionSection);
                }
            }

            // Legacy records array
            if (root.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
            {
                var section = new OutputSection
                {
                    Title = "Entropy Records",
                    Type = "table",
                    Columns = ["Name", "Offset", "Size", "Entropy", "Status"]
                };

                foreach (var record in records.EnumerateArray())
                {
                    section.Rows.Add(
                    [
                        GetString(record, "name"),
                        GetNumber(record, "offset"),
                        GetNumber(record, "size"),
                        GetNumber(record, "entropy", "F4"),
                        GetString(record, "status")
                    ]);
                }

                output.Sections.Add(section);
            }
        }
        catch (JsonException)
        {
            // If output is not valid JSON, display raw output
            output.Sections.Add(new OutputSection
            {
                Title = "Raw Output",
                Type = "text",
                Text = result.StandardOutput
            });
        }

        return output;
    }

    private static string GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value)
            ? value.GetString() ?? value.ToString()
            : "Unknown";
    }

    private static string GetNumber(
        JsonElement element,
        string property,
        string format = "0")
    {
        if (!element.TryGetProperty(property, out var value)
            || !value.TryGetDouble(out var number))
        {
            return "Unknown";
        }

        return number.ToString(format);
    }
}
