using System.Text;
using System.Text.Json;
using BinOcular.Api.Analysis;
using BinOcular.Api.Models;

namespace BinOcular.Api.Tools;

public sealed class BinarySectionsTool(ILogger<BinarySectionsTool> logger) : IAnalysisTool
{
    public string Name => "Binary Sections & Entropy Visualizer";

    public bool IsCompatible(string filePath) => File.Exists(filePath);

    public async Task<ToolResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Inspecting sections and per-section entropy for {FilePath}", filePath);

        try
        {
            await using var stream = File.OpenRead(filePath);
            var sections = new List<SectionInfo>();
            var suspiciousFindings = new List<string>();

            var header = new byte[Math.Min(1024, stream.Length)];
            _ = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);

            // PE Inspection
            if (header.Length >= 0x40 && header[0] == 0x4D && header[1] == 0x5A) // MZ
            {
                var peOffset = BitConverter.ToInt32(header, 0x3C);
                if (peOffset > 0 && peOffset + 24 <= stream.Length)
                {
                    stream.Seek(peOffset, SeekOrigin.Begin);
                    var peSig = new byte[24];
                    _ = await stream.ReadAsync(peSig.AsMemory(0, 24), cancellationToken);

                    if (peSig[0] == 0x50 && peSig[1] == 0x45) // PE\0\0
                    {
                        var numSections = BitConverter.ToUInt16(peSig, 6);
                        var optHeaderSize = BitConverter.ToUInt16(peSig, 20);

                        var sectionTableOffset = peOffset + 24 + optHeaderSize;
                        if (sectionTableOffset + (numSections * 40) <= stream.Length)
                        {
                            stream.Seek(sectionTableOffset, SeekOrigin.Begin);
                            var sectionHeaders = new byte[numSections * 40];
                            _ = await stream.ReadAsync(sectionHeaders.AsMemory(0, sectionHeaders.Length), cancellationToken);

                            for (var i = 0; i < numSections; i++)
                            {
                                var offset = i * 40;
                                var nameRaw = Encoding.UTF8.GetString(sectionHeaders, offset, 8).TrimEnd('\0', ' ');
                                var virtualSize = BitConverter.ToUInt32(sectionHeaders, offset + 8);
                                var virtualAddress = BitConverter.ToUInt32(sectionHeaders, offset + 12);
                                var rawSize = BitConverter.ToUInt32(sectionHeaders, offset + 16);
                                var rawOffset = BitConverter.ToUInt32(sectionHeaders, offset + 20);
                                var characteristics = BitConverter.ToUInt32(sectionHeaders, offset + 36);

                                var isExec = (characteristics & 0x20000000) != 0;
                                var isRead = (characteristics & 0x40000000) != 0;
                                var isWrite = (characteristics & 0x80000000) != 0;

                                var permissions = $"{(isRead ? "R" : "-")}{(isWrite ? "W" : "-")}{(isExec ? "X" : "-")}";

                                // Calculate entropy of raw data
                                var entropy = 0.0;
                                if (rawSize > 0 && rawOffset + rawSize <= stream.Length)
                                {
                                    var sectionBytes = new byte[Math.Min(rawSize, 2_000_000)];
                                    stream.Seek(rawOffset, SeekOrigin.Begin);
                                    var read = await stream.ReadAsync(sectionBytes.AsMemory(0, sectionBytes.Length), cancellationToken);
                                    entropy = CalculateEntropy(sectionBytes, read);
                                }

                                var isSuspicious = false;
                                if (isExec && isWrite)
                                {
                                    suspiciousFindings.Add($"Section '{nameRaw}' has RWX permissions (Read, Write, and Execute). High risk for code injection or unpacking.");
                                    isSuspicious = true;
                                }

                                if (entropy > 7.2)
                                {
                                    suspiciousFindings.Add($"Section '{nameRaw}' has high entropy ({entropy:F2} / 8.0). Strong indicator of packing or encryption.");
                                    isSuspicious = true;
                                }

                                if (rawSize == 0 && virtualSize > 0)
                                {
                                    suspiciousFindings.Add($"Section '{nameRaw}' has zero raw size but allocates {virtualSize} virtual bytes (typical for unpacking stubs like UPX).");
                                    isSuspicious = true;
                                }

                                sections.Add(new SectionInfo
                                {
                                    Name = string.IsNullOrWhiteSpace(nameRaw) ? $".sec{i}" : nameRaw,
                                    VirtualAddress = $"0x{virtualAddress:X8}",
                                    VirtualSize = FormatBytes(virtualSize),
                                    RawSize = FormatBytes(rawSize),
                                    Permissions = permissions,
                                    Entropy = Math.Round(entropy, 4),
                                    Status = isSuspicious ? "Suspicious" : "Normal"
                                });
                            }
                        }
                    }
                }
            }

            var result = new
            {
                format = sections.Count > 0 ? "PE Image Sections" : "Standard Binary",
                totalSections = sections.Count,
                sections,
                suspiciousFindings
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
            logger.LogError(ex, "BinarySectionsTool failed on {FilePath}", filePath);
            return new ToolResult
            {
                Tool = Name,
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = ex.Message
            };
        }
    }

    private static double CalculateEntropy(byte[] data, int length)
    {
        if (length == 0) return 0.0;
        var counts = new int[256];
        for (var i = 0; i < length; i++) counts[data[i]]++;

        var total = (double)length;
        var entropy = 0.0;
        for (var i = 0; i < 256; i++)
        {
            if (counts[i] == 0) continue;
            var p = counts[i] / total;
            entropy -= p * Math.Log2(p);
        }
        return entropy;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    private sealed class SectionInfo
    {
        public required string Name { get; set; }
        public required string VirtualAddress { get; set; }
        public required string VirtualSize { get; set; }
        public required string RawSize { get; set; }
        public required string Permissions { get; set; }
        public double Entropy { get; set; }
        public required string Status { get; set; }
    }
}
