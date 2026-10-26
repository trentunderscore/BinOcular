using BinOcular.Api.Models;

namespace BinOcular.Api.Services;

public sealed class Sha256Formatter : IToolResultFormatter
{
    public string ToolName => "SHA256 Hash Tool";

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
                    ? "No hash calculated."
                    : result.StandardError
            });
            return output;
        }

        output.Sections.Add(new OutputSection
        {
            Title = "Cryptographic Integrity",
            Type = "keyValue",
            Fields =
            [
                new OutputField
                {
                    Label = "SHA-256 Digest",
                    Value = result.StandardOutput.Trim()
                },
                new OutputField
                {
                    Label = "Hash Algorithm",
                    Value = "FIPS 180-4 SHA-256 (256-bit)"
                }
            ]
        });

        return output;
    }
}
