using System.Text.Json;
using BinOcular.Api.Models;

namespace BinOcular.Api.Services;

public sealed class FileFormatFormatter : IToolResultFormatter
{
    public string ToolName => "File Architecture & Format Inspector";

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
                    ? "No format inspection data produced."
                    : result.StandardError
            });
            return output;
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;

            // Section 1: Binary Specs
            var specFields = new List<OutputField>();

            if (root.TryGetProperty("Format", out var fmt))
            {
                specFields.Add(new OutputField { Label = "Container Format", Value = fmt.GetString() ?? "Unknown" });
            }

            if (root.TryGetProperty("Architecture", out var arch))
            {
                specFields.Add(new OutputField { Label = "Target Architecture", Value = arch.GetString() ?? "Unknown" });
            }

            if (root.TryGetProperty("Subsystem", out var sub))
            {
                specFields.Add(new OutputField { Label = "Executable Subsystem", Value = sub.GetString() ?? "Unknown" });
            }

            if (root.TryGetProperty("BitWidth", out var bit))
            {
                specFields.Add(new OutputField { Label = "Bit Width", Value = bit.GetString() ?? "Unknown" });
            }

            if (root.TryGetProperty("Endianness", out var end))
            {
                specFields.Add(new OutputField { Label = "Byte Order", Value = end.GetString() ?? "Unknown" });
            }

            output.Sections.Add(new OutputSection
            {
                Title = "Binary Specification",
                Type = "keyValue",
                Fields = specFields
            });

            // Section 2: Security Mitigations (Tags)
            if (root.TryGetProperty("SecurityMitigations", out var mitigations) &&
                mitigations.ValueKind == JsonValueKind.Array)
            {
                var mitFields = new List<OutputField>();
                foreach (var mit in mitigations.EnumerateArray())
                {
                    mitFields.Add(new OutputField
                    {
                        Label = "Mitigation",
                        Value = mit.GetString() ?? string.Empty
                    });
                }

                if (mitFields.Count > 0)
                {
                    output.Sections.Add(new OutputSection
                    {
                        Title = "Security Mitigations & Hardening",
                        Type = "tags",
                        Fields = mitFields
                    });
                }
            }

            // Section 3: POSIX Description
            if (root.TryGetProperty("PosixDescription", out var posix) &&
                !string.IsNullOrWhiteSpace(posix.GetString()))
            {
                output.Sections.Add(new OutputSection
                {
                    Title = "Operating System Detection",
                    Type = "text",
                    Text = posix.GetString()
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
