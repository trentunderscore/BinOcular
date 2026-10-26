
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

        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;

        var summaryFields = new List<OutputField>();

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
                Value = total.GetDouble().ToString("F4")
            });
        }

        output.Sections.Add(new OutputSection
        {
            Title = "File Summary",
            Type = "keyValue",
            Fields = summaryFields
        });

        if (root.TryGetProperty("records", out var records)
            && records.ValueKind == JsonValueKind.Array)
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

        return output;
    }

    private static string GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value)
            ? value.ToString()
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
