using System.Text.Json;
using BinOcular.Api.Analysis;
using BinOcular.Api.Models;

namespace BinOcular.Api.Tools;

public sealed class OpcodePatternTool(ILogger<OpcodePatternTool> logger) : IAnalysisTool
{
    public string Name => "Assembly & Shellcode Pattern Detector";

    public bool IsCompatible(string filePath) => File.Exists(filePath);

    public async Task<ToolResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Scanning binary assembly and shellcode patterns for {FilePath}", filePath);

        try
        {
            var data = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var findings = new List<OpcodeFinding>();

            // 1. NOP Sled Detection (16+ consecutive 0x90 bytes)
            var nopCount = 0;
            var maxNop = 0;
            long nopStartOffset = -1;
            for (var i = 0; i < data.Length; i++)
            {
                if (data[i] == 0x90)
                {
                    if (nopCount == 0) nopStartOffset = i;
                    nopCount++;
                    if (nopCount > maxNop) maxNop = nopCount;
                }
                else
                {
                    if (nopCount >= 16)
                    {
                        findings.Add(new OpcodeFinding
                        {
                            Pattern = "NOP Sled Sequence",
                            Category = "Exploit / Shellcode",
                            Offset = $"0x{nopStartOffset:X8}",
                            Details = $"{nopCount} consecutive NOP (0x90) instructions detected",
                            Risk = "High"
                        });
                    }
                    nopCount = 0;
                }
            }

            // 2. Direct Syscall Sequences (EDR Hook Evasion)
            // x86_64: mov r10, rcx (49 89 CA); mov eax, <id> (B8 xx xx xx xx); syscall (0F 05); ret (C3)
            for (var i = 0; i + 12 < data.Length; i++)
            {
                if (data[i] == 0x49 && data[i + 1] == 0x89 && data[i + 2] == 0xCA &&
                    data[i + 3] == 0xB8 &&
                    data[i + 8] == 0x0F && data[i + 9] == 0x05)
                {
                    var syscallId = BitConverter.ToInt32(data, i + 4);
                    findings.Add(new OpcodeFinding
                    {
                        Pattern = "Direct System Call (Syscall Stub)",
                        Category = "EDR Evasion / Direct Syscall",
                        Offset = $"0x{i:X8}",
                        Details = $"Direct x64 syscall invocation (SSN: 0x{syscallId:X4}) bypassing userland API hooks",
                        Risk = "Critical"
                    });
                    i += 10;
                }
                // x86: mov eax, <id> (B8 xx xx xx xx); mov edx, esp (89 E2); sysenter (0F 34)
                else if (data[i] == 0xB8 && data[i + 5] == 0x89 && data[i + 6] == 0xE2 &&
                         data[i + 7] == 0x0F && data[i + 8] == 0x34)
                {
                    findings.Add(new OpcodeFinding
                    {
                        Pattern = "Direct Sysenter Stub",
                        Category = "EDR Evasion / Direct Syscall",
                        Offset = $"0x{i:X8}",
                        Details = "Direct x86 sysenter invocation bypassing userland API hooks",
                        Risk = "High"
                    });
                    i += 8;
                }
            }

            // 3. Win32 PEB Access Patterns (Shellcode In-Memory Resolution)
            // 32-bit: fs:[0x30] -> 64 A1 30 00 00 00 or 64 8B ...
            for (var i = 0; i + 6 < data.Length; i++)
            {
                if (data[i] == 0x64 && data[i + 1] == 0xA1 && data[i + 2] == 0x30 && data[i + 3] == 0x00 && data[i + 4] == 0x00 && data[i + 5] == 0x00)
                {
                    findings.Add(new OpcodeFinding
                    {
                        Pattern = "PEB Walk (fs:[0x30])",
                        Category = "Shellcode API Resolution",
                        Offset = $"0x{i:X8}",
                        Details = "Direct 32-bit Process Environment Block retrieval (fs:[0x30]) without API calls",
                        Risk = "High"
                    });
                }
                // 64-bit: gs:[0x60] -> 65 48 8B ... 60
                else if (data[i] == 0x65 && data[i + 1] == 0x48 && data[i + 2] == 0x8B &&
                         (data[i + 3] == 0x04 || data[i + 3] == 0x14 || data[i + 3] == 0x25) &&
                         data[i + 4] == 0x60)
                {
                    findings.Add(new OpcodeFinding
                    {
                        Pattern = "PEB Walk (gs:[0x60])",
                        Category = "Shellcode API Resolution",
                        Offset = $"0x{i:X8}",
                        Details = "Direct 64-bit Process Environment Block retrieval (gs:[0x60]) without API calls",
                        Risk = "High"
                    });
                }
            }

            // 4. Anti-Debugging Instructions
            // RDTSC (0F 31) - Timing checks
            var rdtscCount = 0;
            for (var i = 0; i + 1 < data.Length; i++)
            {
                if (data[i] == 0x0F && data[i + 1] == 0x31)
                {
                    rdtscCount++;
                }
            }
            if (rdtscCount >= 4)
            {
                findings.Add(new OpcodeFinding
                {
                    Pattern = "RDTSC Instruction Clusters",
                    Category = "Anti-Analysis / Timing Checks",
                    Offset = "Multiple",
                    Details = $"{rdtscCount} RDTSC instructions found (frequently used for VM/debugger execution timing detection)",
                    Risk = "Medium"
                });
            }

            // Interrupt-based Anti-Debug: INT 2D (CD 2D) or ICEBP (F1)
            for (var i = 0; i + 1 < data.Length; i++)
            {
                if (data[i] == 0xCD && data[i + 1] == 0x2D)
                {
                    findings.Add(new OpcodeFinding
                    {
                        Pattern = "INT 2D Interrupt Exception",
                        Category = "Anti-Analysis / Exception Manipulation",
                        Offset = $"0x{i:X8}",
                        Details = "Direct INT 2D instruction to disrupt debugger single-stepping",
                        Risk = "High"
                    });
                }
            }

            // 5. Stack Pivot / ROP Gadget Indicators
            // xchg esp, eax (94); ret (C3)
            for (var i = 0; i + 1 < data.Length; i++)
            {
                if (data[i] == 0x94 && data[i + 1] == 0xC3)
                {
                    findings.Add(new OpcodeFinding
                    {
                        Pattern = "Stack Pivot Gadget (xchg esp, eax; ret)",
                        Category = "Exploit / ROP Chain",
                        Offset = $"0x{i:X8}",
                        Details = "Stack pivot primitive commonly leveraged in Return-Oriented Programming (ROP)",
                        Risk = "Medium"
                    });
                }
            }

            var summary = findings.Count switch
            {
                0 => "No suspicious assembly, shellcode stubs, or anti-debug opcodes identified.",
                1 => "1 suspicious assembly / shellcode indicator identified.",
                _ => $"{findings.Count} assembly / shellcode indicators identified."
            };

            var result = new
            {
                totalFindings = findings.Count,
                summary,
                findings
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
            logger.LogError(ex, "OpcodePatternTool failed for {FilePath}", filePath);
            return new ToolResult
            {
                Tool = Name,
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = ex.Message
            };
        }
    }

    private sealed class OpcodeFinding
    {
        public required string Pattern { get; set; }
        public required string Category { get; set; }
        public required string Offset { get; set; }
        public required string Details { get; set; }
        public required string Risk { get; set; }
    }
}
