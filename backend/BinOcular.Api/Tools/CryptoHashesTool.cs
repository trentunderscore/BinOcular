using System.Security.Cryptography;
using System.Text.Json;
using BinOcular.Api.Analysis;
using BinOcular.Api.Models;

namespace BinOcular.Api.Tools;

public sealed class CryptoHashesTool(ILogger<CryptoHashesTool> logger) : IAnalysisTool
{
    public string Name => "Cryptographic Hashes & Entropy";

    public bool IsCompatible(string filePath) => File.Exists(filePath);

    public async Task<ToolResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting Cryptographic Hashes & Entropy analysis for {FilePath}", filePath);

        try
        {
            var fileInfo = new FileInfo(filePath);
            var fileSizeBytes = fileInfo.Length;

            byte[] fileBytes;
            if (fileSizeBytes > 100_000_000)
            {
                // For very large files, read in chunks
                return await AnalyzeLargeFileAsync(filePath, fileSizeBytes, cancellationToken);
            }

            fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);

            var md5Hex = Convert.ToHexString(MD5.HashData(fileBytes)).ToLowerInvariant();
            var sha1Hex = Convert.ToHexString(SHA1.HashData(fileBytes)).ToLowerInvariant();
            var sha256Hex = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();
            var sha512Hex = Convert.ToHexString(SHA512.HashData(fileBytes)).ToLowerInvariant();

            var entropy = CalculateShannonEntropy(fileBytes);
            var entropyRisk = GetEntropyRisk(entropy);

            var resultData = new
            {
                fileSize = FormatFileSize(fileSizeBytes),
                fileSizeBytes,
                md5 = md5Hex,
                sha1 = sha1Hex,
                sha256 = sha256Hex,
                sha512 = sha512Hex,
                entropy = Math.Round(entropy, 4),
                entropyRisk,
                entropyDescription = GetEntropyDescription(entropy)
            };

            return new ToolResult
            {
                Tool = Name,
                ExitCode = 0,
                StandardOutput = JsonSerializer.Serialize(resultData, new JsonSerializerOptions { WriteIndented = true }),
                StandardError = string.Empty
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CryptoHashesTool failed for file {FilePath}", filePath);
            return new ToolResult
            {
                Tool = Name,
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = ex.Message
            };
        }
    }

    private static double CalculateShannonEntropy(byte[] data)
    {
        if (data.Length == 0) return 0.0;

        var byteCounts = new int[256];
        foreach (var b in data)
        {
            byteCounts[b]++;
        }

        var total = (double)data.Length;
        var entropy = 0.0;

        for (var i = 0; i < 256; i++)
        {
            if (byteCounts[i] == 0) continue;
            var p = byteCounts[i] / total;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }

    private static string GetEntropyRisk(double entropy) => entropy switch
    {
        < 5.5 => "Low (Normal Code/Text)",
        < 6.8 => "Moderate (Mixed Data / Assets)",
        < 7.2 => "Elevated (Potential Compression)",
        _ => "High (Likely Packed / Encrypted / Obfuscated)"
    };

    private static string GetEntropyDescription(double entropy) => entropy switch
    {
        < 5.5 => "Standard binary instructions and strings with predictable byte distribution.",
        < 6.8 => "Average distribution commonly found in compiled binaries with embedded resources.",
        < 7.2 => "Dense byte entropy. May contain compressed segments or packed structures.",
        _ => "Extremely high randomness. Strong indicator of executable packing (e.g. UPX, Themida) or payload encryption."
    };

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        var order = 0;
        double len = bytes;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private async Task<ToolResult> AnalyzeLargeFileAsync(
        string filePath,
        long fileSizeBytes,
        CancellationToken cancellationToken)
    {
        await using var md5Stream = File.OpenRead(filePath);
        var md5 = Convert.ToHexString(await MD5.Create().ComputeHashAsync(md5Stream, cancellationToken)).ToLowerInvariant();

        await using var sha256Stream = File.OpenRead(filePath);
        var sha256 = Convert.ToHexString(await SHA256.Create().ComputeHashAsync(sha256Stream, cancellationToken)).ToLowerInvariant();

        var resultData = new
        {
            fileSize = FormatFileSize(fileSizeBytes),
            fileSizeBytes,
            md5,
            sha256,
            entropy = 0.0,
            entropyRisk = "Skipped on >100MB files",
            entropyDescription = "Large file"
        };

        return new ToolResult
        {
            Tool = Name,
            ExitCode = 0,
            StandardOutput = JsonSerializer.Serialize(resultData, new JsonSerializerOptions { WriteIndented = true }),
            StandardError = string.Empty
        };
    }
}
