using System.Text.Json;
using BinOcular.Api.Models;

namespace BinOcular.Api.Services;

public sealed class ImportsExportsFormatter : IToolResultFormatter
{
    public string ToolName => "Imports & Library Dependencies";

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
                    ? "No import data produced."
                    : result.StandardError
            });
            return output;
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;

            // Summary
            var total = root.TryGetProperty("totalDependencies", out var t) ? t.GetInt32() : 0;
            output.Sections.Add(new OutputSection
            {
                Title = "Dependency Overview",
                Type = "keyValue",
                Fields =
                [
                    new OutputField { Label = "Linked Libraries Count", Value = total.ToString() },
                    new OutputField { Label = "Linkage Type", Value = "Dynamic Linking (Shared Libraries)" }
                ]
            });

            // Capabilities Tags
            if (root.TryGetProperty("detectedCapabilities", out var caps) &&
                caps.ValueKind == JsonValueKind.Array &&
                caps.GetArrayLength() > 0)
            {
                var capFields = new List<OutputField>();
                foreach (var c in caps.EnumerateArray())
                {
                    capFields.Add(new OutputField
                    {
                        Label = "Capability",
                        Value = c.GetString() ?? ""
                    });
                }

                output.Sections.Add(new OutputSection
                {
                    Title = "Inferred Binary Capabilities",
                    Type = "tags",
                    Fields = capFields
                });
            }

            // Libraries Table
            if (root.TryGetProperty("libraries", out var libs) && libs.ValueKind == JsonValueKind.Array)
            {
                var rows = new List<List<string>>();
                foreach (var l in libs.EnumerateArray())
                {
                    var libName = l.GetString() ?? "";
                    var category = libName switch
                    {
                        var s when s.Contains("WS2") || s.Contains("INET") || s.Contains("URL") => "Networking",
                        var s when s.Contains("CRYPT") || s.Contains("crypto") || s.Contains("ssl") => "Cryptography",
                        var s when s.Contains("USER") || s.Contains("GDI") => "User Interface",
                        var s when s.Contains("ADVAPI") => "Security / Registry",
                        var s when s.Contains("SHELL") => "Shell / System",
                        _ => "System Runtime"
                    };
                    rows.Add([libName, category, "Dynamically Loaded"]);
                }

                if (rows.Count > 0)
                {
                    output.Sections.Add(new OutputSection
                    {
                        Title = "Linked Dynamic Libraries & DLLs",
                        Type = "table",
                        Columns = ["Library / DLL", "Subsystem Category", "Binding Mode"],
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
