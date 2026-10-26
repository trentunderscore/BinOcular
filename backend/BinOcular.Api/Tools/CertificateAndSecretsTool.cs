using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BinOcular.Api.Analysis;
using BinOcular.Api.Models;

namespace BinOcular.Api.Tools;

public sealed partial class CertificateAndSecretsTool(ILogger<CertificateAndSecretsTool> logger) : IAnalysisTool
{
    public string Name => "Digital Signatures & Secrets Scanner";

    public bool IsCompatible(string filePath) => File.Exists(filePath);

    public async Task<ToolResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Scanning for digital signatures, certificates, and hardcoded secrets in {FilePath}", filePath);

        try
        {
            var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var fileText = Encoding.ASCII.GetString(fileBytes);

            // 1. Authenticode Signature Detection
            var isSigned = false;
            var signatureDetails = "No Authenticode signature directory present.";
            var signatureSize = 0;

            if (fileBytes.Length >= 0x40 && fileBytes[0] == 0x4D && fileBytes[1] == 0x5A) // MZ
            {
                var peOffset = BitConverter.ToInt32(fileBytes, 0x3C);
                if (peOffset > 0 && peOffset + 24 <= fileBytes.Length)
                {
                    var optHeaderOffset = peOffset + 24;
                    if (optHeaderOffset + 152 <= fileBytes.Length)
                    {
                        var magic = BitConverter.ToUInt16(fileBytes, optHeaderOffset);
                        var secDirOffset = magic == 0x20B
                            ? optHeaderOffset + 144 // PE32+ (64-bit)
                            : optHeaderOffset + 128; // PE32 (32-bit)

                        if (secDirOffset + 8 <= fileBytes.Length)
                        {
                            var certVirtualAddr = BitConverter.ToUInt32(fileBytes, secDirOffset);
                            var certSize = BitConverter.ToUInt32(fileBytes, secDirOffset + 4);

                            if (certVirtualAddr > 0 && certSize > 0)
                            {
                                isSigned = true;
                                signatureSize = (int)certSize;
                                signatureDetails = $"Authenticode Signature Directory found at offset 0x{certVirtualAddr:X8} ({certSize} bytes).";
                            }
                        }
                    }
                }
            }

            // 2. Scan for Secrets, Private Keys & Tokens
            var detectedSecrets = new List<SecretFinding>();

            // PEM Certificates
            if (fileText.Contains("-----BEGIN CERTIFICATE-----"))
            {
                detectedSecrets.Add(new SecretFinding
                {
                    Type = "X.509 Certificate",
                    Finding = "Embedded PEM Certificate Header",
                    Severity = "Informational",
                    Description = "Binary includes embedded SSL/TLS certificate"
                });
            }

            // Private Keys
            if (fileText.Contains("-----BEGIN PRIVATE KEY-----") ||
                fileText.Contains("-----BEGIN RSA PRIVATE KEY-----") ||
                fileText.Contains("-----BEGIN EC PRIVATE KEY-----"))
            {
                detectedSecrets.Add(new SecretFinding
                {
                    Type = "Private Key",
                    Finding = "Exposed Private Key Material",
                    Severity = "Critical",
                    Description = "Hardcoded private encryption key embedded in binary"
                });
            }

            // AWS Access Keys
            var awsMatches = AwsRegex().Matches(fileText);
            foreach (Match match in awsMatches)
            {
                detectedSecrets.Add(new SecretFinding
                {
                    Type = "Cloud Credentials",
                    Finding = RedactSecret(match.Value),
                    Severity = "Critical",
                    Description = "Hardcoded AWS Access Key ID detected"
                });
            }

            // GitHub Tokens
            var ghMatches = GitHubRegex().Matches(fileText);
            foreach (Match match in ghMatches)
            {
                detectedSecrets.Add(new SecretFinding
                {
                    Type = "API Token",
                    Finding = RedactSecret(match.Value),
                    Severity = "High",
                    Description = "GitHub Personal Access Token"
                });
            }

            // JSON Web Tokens (JWT)
            var jwtMatches = JwtRegex().Matches(fileText);
            foreach (Match match in jwtMatches.Take(5))
            {
                detectedSecrets.Add(new SecretFinding
                {
                    Type = "Authentication Token",
                    Finding = RedactSecret(match.Value),
                    Severity = "Medium",
                    Description = "Embedded JWT Bearer Token"
                });
            }

            var result = new
            {
                isDigitallySigned = isSigned,
                signatureStatus = isSigned ? "Digitally Signed (Authenticode)" : "Unsigned Binary",
                signatureSize = signatureSize > 0 ? $"{signatureSize} bytes" : "N/A",
                signatureDetails,
                totalSecretsFound = detectedSecrets.Count,
                detectedSecrets
            };

            return new ToolResult
            {
                Tool = Name,
                ExitCode = 0,
                StandardOutput = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }),
                StandardError = string.Empty
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CertificateAndSecretsTool failed for {FilePath}", filePath);
            return new ToolResult
            {
                Tool = Name,
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = ex.Message
            };
        }
    }

    private static string RedactSecret(string secret)
    {
        if (secret.Length <= 8) return "********";
        return $"{secret[..4]}...{secret[^4..]}";
    }

    [GeneratedRegex(@"\bAKIA[0-9A-Z]{16}\b")]
    private static partial Regex AwsRegex();

    [GeneratedRegex(@"\bghp_[a-zA-Z0-9]{36}\b")]
    private static partial Regex GitHubRegex();

    [GeneratedRegex(@"\beyJ[a-zA-Z0-9_-]{10,}\.eyJ[a-zA-Z0-9_-]{10,}\.[a-zA-Z0-9_-]{10,}\b")]
    private static partial Regex JwtRegex();

    private sealed class SecretFinding
    {
        public required string Type { get; set; }
        public required string Finding { get; set; }
        public required string Severity { get; set; }
        public required string Description { get; set; }
    }
}
