using System.Text.Json;
using BinOcular.Api.Models;

namespace BinOcular.Api.Services;

public sealed class CryptoHashesFormatter : IToolResultFormatter
{
    public string ToolName => "Cryptographic Hashes & Entropy";

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
                    ? "No hash data produced."
                    : result.StandardError
            });
            return output;
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;

            // Section 1: Cryptographic Hashes
            var hashFields = new List<OutputField>();

            if (root.TryGetProperty("fileSize", out var fileSize))
            {
                hashFields.Add(new OutputField { Label = "File Size", Value = fileSize.GetString() ?? "Unknown" });
            }

            if (root.TryGetProperty("md5", out var md5))
            {
                hashFields.Add(new OutputField { Label = "MD5", Value = md5.GetString() ?? "Unknown" });
            }

            if (root.TryGetProperty("sha1", out var sha1))
            {
                hashFields.Add(new OutputField { Label = "SHA-1", Value = sha1.GetString() ?? "Unknown" });
            }

            if (root.TryGetProperty("sha256", out var sha256))
            {
                hashFields.Add(new OutputField { Label = "SHA-256", Value = sha256.GetString() ?? "Unknown" });
            }

            if (root.TryGetProperty("sha512", out var sha512))
            {
                hashFields.Add(new OutputField { Label = "SHA-512", Value = sha512.GetString() ?? "Unknown" });
            }

            output.Sections.Add(new OutputSection
            {
                Title = "Cryptographic Digests",
                Type = "keyValue",
                Fields = hashFields
            });

            // Section 2: Shannon Entropy
            var entropyFields = new List<OutputField>();

            if (root.TryGetProperty("entropy", out var entropy))
            {
                entropyFields.Add(new OutputField
                {
                    Label = "Total Shannon Entropy",
                    Value = entropy.GetDouble().ToString("F4")
                });
            }

            if (root.TryGetProperty("entropyRisk", out var risk))
            {
                entropyFields.Add(new OutputField
                {
                    Label = "Entropy Risk Level",
                    Value = risk.GetString() ?? "Unknown"
                });
            }

            if (root.TryGetProperty("entropyDescription", out var desc))
            {
                entropyFields.Add(new OutputField
                {
                    Label = "Heuristic Assessment",
                    Value = desc.GetString() ?? "Unknown"
                });
            }

            output.Sections.Add(new OutputSection
            {
                Title = "Entropy & Packing Assessment",
                Type = "keyValue",
                Fields = entropyFields
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
