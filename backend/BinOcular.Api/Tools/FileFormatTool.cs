using System.Text.Json;
using BinOcular.Api.Analysis;
using BinOcular.Api.Models;

namespace BinOcular.Api.Tools;

public sealed class FileFormatTool(
    ToolRunner toolRunner,
    ILogger<FileFormatTool> logger) : IAnalysisTool
{
    public string Name => "File Architecture & Format Inspector";

    public bool IsCompatible(string filePath) => File.Exists(filePath);

    public async Task<ToolResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Analyzing binary format and architecture for {FilePath}", filePath);

        try
        {
            await using var stream = File.OpenRead(filePath);
            var headerBytes = new byte[Math.Min(1024, stream.Length)];
            _ = await stream.ReadAsync(headerBytes.AsMemory(0, headerBytes.Length), cancellationToken);

            var inspection = InspectBinary(headerBytes, stream.Length);

            // Also attempt to get the system "file" command description
            try
            {
                var fileCmdResult = await toolRunner.RunAsync("/usr/bin/file", ["-b", filePath], cancellationToken);
                if (fileCmdResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(fileCmdResult.StandardOutput))
                {
                    inspection.PosixDescription = fileCmdResult.StandardOutput.Trim();
                }
            }
            catch
            {
                // Non-critical if "file" binary is unavailable
            }

            var json = JsonSerializer.Serialize(inspection, new JsonSerializerOptions { WriteIndented = true });

            return new ToolResult
            {
                Tool = Name,
                ExitCode = 0,
                StandardOutput = json,
                StandardError = string.Empty
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FileFormatTool failed on {FilePath}", filePath);
            return new ToolResult
            {
                Tool = Name,
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = ex.Message
            };
        }
    }

    private static BinaryInspection InspectBinary(byte[] bytes, long totalSize)
    {
        var info = new BinaryInspection
        {
            FileSizeBytes = totalSize,
            Format = "Unknown / Binary Blob",
            Architecture = "Unknown",
            Subsystem = "Unknown",
            Endianness = "Little Endian"
        };

        if (bytes.Length < 4) return info;

        // Check Windows PE
        if (bytes[0] == 0x4D && bytes[1] == 0x5A) // "MZ"
        {
            info.Format = "PE (Portable Executable)";
            info.Endianness = "Little Endian";

            if (bytes.Length >= 0x40)
            {
                var peOffset = BitConverter.ToInt32(bytes, 0x3C);
                if (peOffset > 0 && peOffset + 24 <= bytes.Length)
                {
                    if (bytes[peOffset] == 0x50 && bytes[peOffset + 1] == 0x45) // "PE\0\0"
                    {
                        var machine = BitConverter.ToUInt16(bytes, peOffset + 4);
                        var characteristics = BitConverter.ToUInt16(bytes, peOffset + 22);

                        info.Architecture = machine switch
                        {
                            0x8664 => "x86-64 (AMD64)",
                            0x014c => "x86 (32-bit i386)",
                            0xAA64 => "ARM64 (AArch64)",
                            0x01c0 => "ARM (32-bit)",
                            _ => $"Unknown Machine (0x{machine:X4})"
                        };

                        var isDll = (characteristics & 0x2000) != 0;
                        info.Subsystem = isDll ? "Dynamic Link Library (DLL)" : "Executable (EXE)";

                        // Check Optional Header for Subsystem & Mitigations
                        var optHeaderOffset = peOffset + 24;
                        if (optHeaderOffset + 70 <= bytes.Length)
                        {
                            var magic = BitConverter.ToUInt16(bytes, optHeaderOffset);
                            var is64Bit = magic == 0x20B;
                            info.BitWidth = is64Bit ? "64-bit" : "32-bit";

                            var subsystem = BitConverter.ToUInt16(bytes, optHeaderOffset + 68);
                            info.Subsystem = subsystem switch
                            {
                                1 => "Native Driver / Kernel",
                                2 => isDll ? "Windows GUI (DLL)" : "Windows GUI Application",
                                3 => isDll ? "Windows Console (DLL)" : "Windows Console (CLI)",
                                _ => info.Subsystem
                            };

                            var dllChars = BitConverter.ToUInt16(bytes, optHeaderOffset + 70);
                            if ((dllChars & 0x0040) != 0) info.SecurityMitigations.Add("ASLR (Dynamic Base)");
                            if ((dllChars & 0x0100) != 0) info.SecurityMitigations.Add("DEP / NX (Data Execution Prevention)");
                            if ((dllChars & 0x4000) != 0) info.SecurityMitigations.Add("CFG (Control Flow Guard)");
                            if ((dllChars & 0x0020) != 0) info.SecurityMitigations.Add("High Entropy VA (64-bit ASLR)");
                            if ((dllChars & 0x0400) != 0) info.SecurityMitigations.Add("No SEH (Safe Structured Exception Handling)");
                        }
                    }
                }
            }
            return info;
        }

        // Check Linux ELF
        if (bytes[0] == 0x7F && bytes[1] == 0x45 && bytes[2] == 0x4C && bytes[3] == 0x46) // "\x7fELF"
        {
            info.Format = "ELF (Executable and Linkable Format)";
            info.BitWidth = bytes[4] == 1 ? "32-bit" : bytes[4] == 2 ? "64-bit" : "Unknown";
            info.Endianness = bytes[5] == 1 ? "Little Endian" : bytes[5] == 2 ? "Big Endian" : "Unknown";

            if (bytes.Length >= 20)
            {
                var type = BitConverter.ToUInt16(bytes, 16);
                var machine = BitConverter.ToUInt16(bytes, 18);

                info.Subsystem = type switch
                {
                    1 => "Relocatable Object File",
                    2 => "Executable (EXEC)",
                    3 => "Shared Object (DYN / PIE)",
                    4 => "Core Dump",
                    _ => "Unknown ELF Type"
                };

                info.Architecture = machine switch
                {
                    0x3E => "x86-64 (AMD64)",
                    0x03 => "x86 (i386)",
                    0x28 => "ARM (32-bit)",
                    0xB7 => "ARM64 (AArch64)",
                    0x08 => "MIPS",
                    0xF3 => "RISC-V",
                    _ => $"Machine (0x{machine:X4})"
                };

                if (type == 3) info.SecurityMitigations.Add("PIE (Position Independent Executable)");
            }
            return info;
        }

        // Check Apple Mach-O
        if ((bytes[0] == 0xFE && bytes[1] == 0xED && bytes[2] == 0xFA && (bytes[3] == 0xCE || bytes[3] == 0xCF)) ||
            (bytes[3] == 0xFE && bytes[2] == 0xED && bytes[1] == 0xFA && (bytes[0] == 0xCE || bytes[0] == 0xCF)))
        {
            info.Format = "Mach-O (macOS / iOS)";
            var is64 = bytes[3] == 0xCF || bytes[0] == 0xCF;
            info.BitWidth = is64 ? "64-bit" : "32-bit";

            if (bytes.Length >= 16)
            {
                var cputype = BitConverter.ToInt32(bytes, 4);
                info.Architecture = cputype switch
                {
                    0x0100000C => "Apple Silicon (ARM64)",
                    0x01000007 => "Intel (x86_64)",
                    0x00000007 => "Intel (i386)",
                    _ => $"CPU Type (0x{cputype:X8})"
                };

                var filetype = BitConverter.ToInt32(bytes, 12);
                info.Subsystem = filetype switch
                {
                    2 => "Mach-O Executable",
                    6 => "Dynamic Library (Dylib)",
                    8 => "Bundle",
                    _ => "Mach-O Object"
                };

                info.SecurityMitigations.Add("PIE Enabled");
            }
            return info;
        }

        // Check Archives & Firmware
        if (bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04)
        {
            info.Format = "ZIP / Compressed Archive (JAR/APK/DOCX)";
            info.Subsystem = "Archive / Container";
            return info;
        }

        if (bytes[0] == 0x1F && bytes[1] == 0x8B)
        {
            info.Format = "GZIP Compressed Archive";
            info.Subsystem = "Archive";
            return info;
        }

        if (bytes.Length >= 16 && System.Text.Encoding.ASCII.GetString(bytes, 0, 15).StartsWith("SQLite format 3"))
        {
            info.Format = "SQLite 3 Database File";
            info.Subsystem = "Database Storage";
            return info;
        }

        if (bytes.Length >= 5 && System.Text.Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-")
        {
            info.Format = "Adobe Portable Document Format (PDF)";
            info.Subsystem = "Document";
            return info;
        }

        return info;
    }

    private sealed class BinaryInspection
    {
        public long FileSizeBytes { get; set; }
        public string Format { get; set; } = string.Empty;
        public string Architecture { get; set; } = string.Empty;
        public string Subsystem { get; set; } = string.Empty;
        public string BitWidth { get; set; } = "Unknown";
        public string Endianness { get; set; } = "Little Endian";
        public string? PosixDescription { get; set; }
        public List<string> SecurityMitigations { get; set; } = [];
    }
}
