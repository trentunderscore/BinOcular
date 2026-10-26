using System.Text.Json;
using BinOcular.Api.Analysis;
using BinOcular.Api.Models;

namespace BinOcular.Api.Tools;

public sealed class EmbeddedAssetsTool(ILogger<EmbeddedAssetsTool> logger) : IAnalysisTool
{
    public string Name => "Embedded Files & Firmware Payload Scanner";

    public bool IsCompatible(string filePath) => File.Exists(filePath);

    public async Task<ToolResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Scanning binary for embedded payloads and firmware images in {FilePath}", filePath);

        try
        {
            var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var assets = new List<EmbeddedAsset>();

            // 1. Scan for secondary embedded PE executables (offset > 1024)
            for (var i = 1024; i + 64 < fileBytes.Length; i += 2)
            {
                if (fileBytes[i] == 0x4D && fileBytes[i + 1] == 0x5A) // "MZ"
                {
                    var peOffset = BitConverter.ToInt32(fileBytes, i + 0x3C);
                    if (peOffset > 0 && i + peOffset + 4 < fileBytes.Length)
                    {
                        if (fileBytes[i + peOffset] == 0x50 && fileBytes[i + peOffset + 1] == 0x45) // "PE\0\0"
                        {
                            assets.Add(new EmbeddedAsset
                            {
                                Type = "Embedded PE Executable (Dropper Payload)",
                                Offset = $"0x{i:X8}",
                                Severity = "Critical (Secondary Binary)",
                                Description = "Nested executable image embedded inside payload body (common dropper behavior)"
                            });
                            i += 4096; // skip ahead
                        }
                    }
                }
            }

            // 2. Scan for embedded ZIP archives (offset > 0)
            byte[] zipSig = [0x50, 0x4B, 0x03, 0x04];
            FindSignatures(fileBytes, zipSig, 1, (offset) =>
            {
                assets.Add(new EmbeddedAsset
                {
                    Type = "Embedded ZIP / JAR Archive",
                    Offset = $"0x{offset:X8}",
                    Severity = "Medium",
                    Description = "Compressed ZIP container or Java archive embedded in binary"
                });
            });

            // 3. Scan for embedded GZIP streams (offset > 0)
            byte[] gzipSig = [0x1F, 0x8B, 0x08];
            FindSignatures(fileBytes, gzipSig, 1, (offset) =>
            {
                assets.Add(new EmbeddedAsset
                {
                    Type = "Embedded GZIP Stream",
                    Offset = $"0x{offset:X8}",
                    Severity = "Low",
                    Description = "Deflate compressed data stream"
                });
            });

            // 4. Scan for embedded SQLite database
            byte[] sqliteSig = [0x53, 0x51, 0x4C, 0x69, 0x74, 0x65, 0x20, 0x66, 0x6F, 0x72, 0x6D, 0x61, 0x74, 0x20, 0x33, 0x00];
            FindSignatures(fileBytes, sqliteSig, 1, (offset) =>
            {
                assets.Add(new EmbeddedAsset
                {
                    Type = "Embedded SQLite Database",
                    Offset = $"0x{offset:X8}",
                    Severity = "Low",
                    Description = "Relational database file structure"
                });
            });

            // 5. Scan for embedded PNG images
            byte[] pngSig = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
            FindSignatures(fileBytes, pngSig, 1, (offset) =>
            {
                assets.Add(new EmbeddedAsset
                {
                    Type = "Embedded PNG Image",
                    Offset = $"0x{offset:X8}",
                    Severity = "Low",
                    Description = "Portable Network Graphics asset"
                });
            });

            // 6. Scan for SquashFS firmware
            byte[] squashfsSig = [0x68, 0x73, 0x71, 0x73]; // "hsqs"
            FindSignatures(fileBytes, squashfsSig, 0, (offset) =>
            {
                assets.Add(new EmbeddedAsset
                {
                    Type = "SquashFS Linux Filesystem",
                    Offset = $"0x{offset:X8}",
                    Severity = "High",
                    Description = "Read-only compressed Linux root filesystem"
                });
            });

            var result = new
            {
                totalAssetsFound = assets.Count,
                hasDropperPayload = assets.Any(a => a.Severity.StartsWith("Critical")),
                summary = assets.Count > 0
                    ? $"Detected {assets.Count} embedded assets or nested container signatures."
                    : "No secondary embedded executables or compressed containers found.",
                assets
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
            logger.LogError(ex, "EmbeddedAssetsTool failed for {FilePath}", filePath);
            return new ToolResult
            {
                Tool = Name,
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = ex.Message
            };
        }
    }

    private static void FindSignatures(byte[] source, byte[] pattern, int minOffset, Action<int> onFound)
    {
        var foundCount = 0;
        for (var i = minOffset; i <= source.Length - pattern.Length && foundCount < 10; i++)
        {
            var match = true;
            for (var j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                onFound(i);
                foundCount++;
                i += pattern.Length + 16; // advance
            }
        }
    }

    private sealed class EmbeddedAsset
    {
        public required string Type { get; set; }
        public required string Offset { get; set; }
        public required string Severity { get; set; }
        public required string Description { get; set; }
    }
}
