using System.Text.Json;
using BinOcular.Api.Models;

namespace BinOcular.Api.Services;

public sealed class CertificateAndSecretsFormatter : IToolResultFormatter
{
    public string ToolName => "Digital Signatures & Secrets Scanner";

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
                    ? "No signature analysis data produced."
                    : result.StandardError
            });
            return output;
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;

            // Signature Overview
            var sigFields = new List<OutputField>();

            if (root.TryGetProperty("signatureStatus", out var status))
            {
                sigFields.Add(new OutputField { Label = "Authenticode Integrity", Value = status.GetString() ?? "Unknown" });
            }

            if (root.TryGetProperty("signatureSize", out var size))
            {
                sigFields.Add(new OutputField { Label = "Signature Table Size", Value = size.GetString() ?? "N/A" });
            }

            if (root.TryGetProperty("signatureDetails", out var details))
            {
                sigFields.Add(new OutputField { Label = "Certificate Location", Value = details.GetString() ?? "" });
            }

            output.Sections.Add(new OutputSection
            {
                Title = "Code Signing & Integrity Verification",
                Type = "keyValue",
                Fields = sigFields
            });

            // Secrets Table
            if (root.TryGetProperty("detectedSecrets", out var secList) && secList.ValueKind == JsonValueKind.Array)
            {
                var rows = new List<List<string>>();

                foreach (var s in secList.EnumerateArray())
                {
                    var type = s.TryGetProperty("Type", out var t) ? t.GetString() ?? "" : "";
                    var finding = s.TryGetProperty("Finding", out var f) ? f.GetString() ?? "" : "";
                    var sev = s.TryGetProperty("Severity", out var sv) ? sv.GetString() ?? "" : "";
                    var desc = s.TryGetProperty("Description", out var d) ? d.GetString() ?? "" : "";

                    rows.Add([type, finding, sev, desc]);
                }

                if (rows.Count > 0)
                {
                    output.Sections.Add(new OutputSection
                    {
                        Title = "Detected Embedded Certificates & Secret Credentials",
                        Type = "table",
                        Columns = ["Category", "Artifact / Redacted Token", "Severity", "Description"],
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
