using System.Text.Json;
using BinOcular.Api.Models;

namespace BinOcular.Api.Services;

public sealed class CompilerAndTimestampFormatter : IToolResultFormatter
{
    public string ToolName => "Build Environment & Compiler Fingerprinter";

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
                    ? "No compiler fingerprint produced."
                    : result.StandardError
            });
            return output;
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;

            // Section 1: Compiler & Tooling
            var envFields = new List<OutputField>();

            if (root.TryGetProperty("compiler", out var comp))
            {
                envFields.Add(new OutputField { Label = "Identified Compiler", Value = comp.GetString() ?? "Unknown" });
            }

            if (root.TryGetProperty("framework", out var fw))
            {
                envFields.Add(new OutputField { Label = "Target Runtime", Value = fw.GetString() ?? "Native" });
            }

            if (root.TryGetProperty("richHeaderDetails", out var rich))
            {
                envFields.Add(new OutputField { Label = "MSVC Rich Header", Value = rich.GetString() ?? "" });
            }

            output.Sections.Add(new OutputSection
            {
                Title = "Build Toolchain & Language Fingerprint",
                Type = "keyValue",
                Fields = envFields
            });

            // Section 2: Timestamp & Integrity
            var timeFields = new List<OutputField>();

            if (root.TryGetProperty("buildTimestamp", out var ts))
            {
                timeFields.Add(new OutputField { Label = "COFF TimeDateStamp", Value = ts.GetString() ?? "Unknown" });
            }

            if (root.TryGetProperty("timestampAnomaly", out var anom))
            {
                timeFields.Add(new OutputField { Label = "Timestamp Anomaly Status", Value = anom.GetString() ?? "None" });
            }

            output.Sections.Add(new OutputSection
            {
                Title = "Compilation Date & Temporal Integrity",
                Type = "keyValue",
                Fields = timeFields
            });
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
