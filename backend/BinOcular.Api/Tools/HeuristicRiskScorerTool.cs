using System.Text;
using System.Text.Json;
using BinOcular.Api.Analysis;
using BinOcular.Api.Models;

namespace BinOcular.Api.Tools;

public sealed class HeuristicRiskScorerTool(ILogger<HeuristicRiskScorerTool> logger) : IAnalysisTool
{
    public string Name => "Heuristic Threat & Anomaly Scorer";

    public bool IsCompatible(string filePath) => File.Exists(filePath);

    public async Task<ToolResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Computing heuristic threat risk score for {FilePath}", filePath);

        try
        {
            var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var fileText = Encoding.ASCII.GetString(fileBytes);

            var riskScore = 0;
            var riskFactors = new List<RiskFactor>();

            // 1. Check for Shannon Entropy
            var entropy = CalculateEntropy(fileBytes);
            if (entropy >= 7.2)
            {
                riskScore += 20;
                riskFactors.Add(new RiskFactor
                {
                    Factor = "Dense Byte Entropy",
                    Impact = "+20",
                    Finding = $"High entropy score ({entropy:F2} / 8.0)",
                    Severity = "High"
                });
            }
            else if (entropy >= 6.8)
            {
                riskScore += 10;
                riskFactors.Add(new RiskFactor
                {
                    Factor = "Moderate Byte Entropy",
                    Impact = "+10",
                    Finding = $"Elevated entropy score ({entropy:F2} / 8.0)",
                    Severity = "Medium"
                });
            }

            // 2. Check for Packer Names
            string[] knownPackers = ["UPX0", "UPX1", "UPX2", ".aspack", "mpress", "Themida", "VMProtect"];
            foreach (var packer in knownPackers)
            {
                if (fileText.Contains(packer, StringComparison.OrdinalIgnoreCase))
                {
                    riskScore += 20;
                    riskFactors.Add(new RiskFactor
                    {
                        Factor = "Known Packer Signature",
                        Impact = "+20",
                        Finding = $"Signature matching packer '{packer}'",
                        Severity = "High"
                    });
                    break;
                }
            }

            // 3. Check for Process Injection APIs
            string[] injectionApis = ["VirtualAlloc", "WriteProcessMemory", "CreateRemoteThread", "QueueUserAPC", "SetWindowsHookEx"];
            var detectedInjectionApis = injectionApis.Where(api => fileText.Contains(api, StringComparison.OrdinalIgnoreCase)).ToList();
            if (detectedInjectionApis.Count > 0)
            {
                var impact = Math.Min(30, detectedInjectionApis.Count * 10);
                riskScore += impact;
                riskFactors.Add(new RiskFactor
                {
                    Factor = "Process Injection APIs",
                    Impact = $"+{impact}",
                    Finding = $"Contains {string.Join(", ", detectedInjectionApis)}",
                    Severity = "Critical"
                });
            }

            // 4. Check for Anti-Debugging & Evasion
            string[] evasionApis = ["IsDebuggerPresent", "CheckRemoteDebuggerPresent", "amsi.dll", "OutputDebugStringA"];
            var detectedEvasionApis = evasionApis.Where(api => fileText.Contains(api, StringComparison.OrdinalIgnoreCase)).ToList();
            if (detectedEvasionApis.Count > 0)
            {
                riskScore += 15;
                riskFactors.Add(new RiskFactor
                {
                    Factor = "Anti-Analysis / Evasion",
                    Impact = "+15",
                    Finding = $"Contains {string.Join(", ", detectedEvasionApis)}",
                    Severity = "Medium"
                });
            }

            // 5. Check for Shell Execution
            if (fileText.Contains("cmd.exe", StringComparison.OrdinalIgnoreCase) ||
                fileText.Contains("powershell", StringComparison.OrdinalIgnoreCase) ||
                fileText.Contains("/bin/sh", StringComparison.OrdinalIgnoreCase))
            {
                riskScore += 15;
                riskFactors.Add(new RiskFactor
                {
                    Factor = "Interactive Shell Invocation",
                    Impact = "+15",
                    Finding = "References command line interpreters (cmd/powershell/sh)",
                    Severity = "High"
                });
            }

            // 6. Check for Embedded Secondary Executables (Dropper)
            for (var i = 2048; i + 64 < fileBytes.Length; i += 4)
            {
                if (fileBytes[i] == 0x4D && fileBytes[i + 1] == 0x5A)
                {
                    var pe = BitConverter.ToInt32(fileBytes, i + 0x3C);
                    if (pe > 0 && i + pe + 4 < fileBytes.Length && fileBytes[i + pe] == 0x50 && fileBytes[i + pe + 1] == 0x45)
                    {
                        riskScore += 30;
                        riskFactors.Add(new RiskFactor
                        {
                            Factor = "Embedded Secondary Binary",
                            Impact = "+30",
                            Finding = $"Secondary PE header identified at offset 0x{i:X8} (Dropper indicator)",
                            Severity = "Critical"
                        });
                        break;
                    }
                }
            }

            // Clamp risk score to 100
            riskScore = Math.Clamp(riskScore, 0, 100);

            var verdict = riskScore switch
            {
                >= 70 => "Critical Threat Risk (Strong Malicious Potential)",
                >= 45 => "Elevated Suspicion (Multiple High-Risk Indicators)",
                >= 20 => "Moderate Risk (Packed or Evasive Characteristics)",
                _ => "Low Risk (Standard Benign Characteristics)"
            };

            var result = new
            {
                threatScore = riskScore,
                verdict,
                riskLevel = riskScore >= 70 ? "Critical" : riskScore >= 45 ? "High" : riskScore >= 20 ? "Medium" : "Low",
                totalFactors = riskFactors.Count,
                riskFactors
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
            logger.LogError(ex, "HeuristicRiskScorerTool failed for {FilePath}", filePath);
            return new ToolResult
            {
                Tool = Name,
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = ex.Message
            };
        }
    }

    private static double CalculateEntropy(byte[] data)
    {
        if (data.Length == 0) return 0.0;
        var counts = new int[256];
        foreach (var b in data) counts[b]++;
        var total = (double)data.Length;
        var entropy = 0.0;
        for (var i = 0; i < 256; i++)
        {
            if (counts[i] == 0) continue;
            var p = counts[i] / total;
            entropy -= p * Math.Log2(p);
        }
        return entropy;
    }

    private sealed class RiskFactor
    {
        public required string Factor { get; set; }
        public required string Impact { get; set; }
        public required string Finding { get; set; }
        public required string Severity { get; set; }
    }
}
