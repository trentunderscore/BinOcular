using System.Text;
using System.Text.Json;
using BinOcular.Api.Analysis;
using BinOcular.Api.Models;

namespace BinOcular.Api.Tools;

public sealed class CryptoScannerTool(ILogger<CryptoScannerTool> logger) : IAnalysisTool
{
    public string Name => "Cryptographic Algorithm & Constant Detector";

    public bool IsCompatible(string filePath) => File.Exists(filePath);

    public async Task<ToolResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Scanning binary for cryptographic constants and algorithms in {FilePath}", filePath);

        try
        {
            var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var detections = new List<CryptoDetection>();

            // 1. AES Forward S-Box (first 16 bytes: 0x63, 0x7c, 0x77, 0x7b, 0xf2, 0x6b, 0x6f, 0xc5, 0x30, 0x01, 0x67, 0x2b, 0xfe, 0xd7, 0xab, 0x76)
            byte[] aesSbox = [0x63, 0x7c, 0x77, 0x7b, 0xf2, 0x6b, 0x6f, 0xc5, 0x30, 0x01, 0x67, 0x2b, 0xfe, 0xd7, 0xab, 0x76];
            var aesOffset = IndexOfBytes(fileBytes, aesSbox);
            if (aesOffset != -1)
            {
                detections.Add(new CryptoDetection
                {
                    Algorithm = "AES (Advanced Encryption Standard)",
                    PatternType = "Forward Substitution Box (S-Box)",
                    Offset = $"0x{aesOffset:X8}",
                    Confidence = "Very High (Exact S-Box match)"
                });
            }

            // 2. AES Inverse S-Box (0x52, 0x09, 0x6a, 0xd5, 0x30, 0x36, 0xa5, 0x38, 0xbf, 0x40, 0xa3, 0x9e, 0x81, 0xf3, 0xd7, 0xfb)
            byte[] aesInvSbox = [0x52, 0x09, 0x6a, 0xd5, 0x30, 0x36, 0xa5, 0x38, 0xbf, 0x40, 0xa3, 0x9e, 0x81, 0xf3, 0xd7, 0xfb];
            var aesInvOffset = IndexOfBytes(fileBytes, aesInvSbox);
            if (aesInvOffset != -1)
            {
                detections.Add(new CryptoDetection
                {
                    Algorithm = "AES Decryption (InvS-Box)",
                    PatternType = "Inverse Substitution Box",
                    Offset = $"0x{aesInvOffset:X8}",
                    Confidence = "Very High"
                });
            }

            // 3. ChaCha20 / Salsa20 Constant ("expand 32-byte k")
            byte[] chachaConst = Encoding.ASCII.GetBytes("expand 32-byte k");
            var chachaOffset = IndexOfBytes(fileBytes, chachaConst);
            if (chachaOffset != -1)
            {
                detections.Add(new CryptoDetection
                {
                    Algorithm = "ChaCha20 / Salsa20 Stream Cipher",
                    PatternType = "Magic State Constant ('expand 32-byte k')",
                    Offset = $"0x{chachaOffset:X8}",
                    Confidence = "Definite (Signature match)"
                });
            }

            // 4. SHA-256 Constants (first 16 bytes of K round constants)
            // 0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5
            byte[] sha256K = [0x98, 0x2f, 0x8a, 0x42, 0x91, 0x44, 0x37, 0x71, 0xcf, 0xfb, 0xc0, 0xb5, 0xa5, 0xdb, 0xb5, 0xe9];
            var sha256Offset = IndexOfBytes(fileBytes, sha256K);
            if (sha256Offset != -1)
            {
                detections.Add(new CryptoDetection
                {
                    Algorithm = "SHA-256 Digest",
                    PatternType = "Round Constants (K Table)",
                    Offset = $"0x{sha256Offset:X8}",
                    Confidence = "Very High"
                });
            }

            // 5. MD5 Initialization Constants (0x67452301, 0xEFCDAB89, 0x98BADCFE, 0x10325476)
            byte[] md5Iv = [0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0xfe, 0xdc, 0xba, 0x98, 0x76, 0x54, 0x32, 0x10];
            var md5Offset = IndexOfBytes(fileBytes, md5Iv);
            if (md5Offset != -1)
            {
                detections.Add(new CryptoDetection
                {
                    Algorithm = "MD5 Hash Function",
                    PatternType = "Initialization Vector (A,B,C,D Registers)",
                    Offset = $"0x{md5Offset:X8}",
                    Confidence = "High"
                });
            }

            // 6. Base64 Character Table
            byte[] base64Table = Encoding.ASCII.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/");
            var b64Offset = IndexOfBytes(fileBytes, base64Table);
            if (b64Offset != -1)
            {
                detections.Add(new CryptoDetection
                {
                    Algorithm = "Base64 Encoding / Decoding",
                    PatternType = "Standard Alphabet Lookup Table",
                    Offset = $"0x{b64Offset:X8}",
                    Confidence = "Definite"
                });
            }

            // 7. CRC32 Polynomial Lookup Table
            byte[] crc32Sub = [0x00, 0x00, 0x00, 0x00, 0x96, 0x30, 0x07, 0x77, 0x2c, 0x61, 0x0e, 0xee];
            var crcOffset = IndexOfBytes(fileBytes, crc32Sub);
            if (crcOffset != -1)
            {
                detections.Add(new CryptoDetection
                {
                    Algorithm = "CRC32 Checksum",
                    PatternType = "Polynomial Table",
                    Offset = $"0x{crcOffset:X8}",
                    Confidence = "High"
                });
            }

            var result = new
            {
                totalDetected = detections.Count,
                hasCryptography = detections.Count > 0,
                summary = detections.Count > 0
                    ? $"Found {detections.Count} cryptographic constant signatures."
                    : "No standard cryptographic lookup tables or S-Boxes detected.",
                detections
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
            logger.LogError(ex, "CryptoScannerTool failed for {FilePath}", filePath);
            return new ToolResult
            {
                Tool = Name,
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = ex.Message
            };
        }
    }

    private static int IndexOfBytes(byte[] source, byte[] pattern)
    {
        if (pattern.Length == 0 || source.Length < pattern.Length) return -1;
        for (var i = 0; i <= source.Length - pattern.Length; i++)
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
            if (match) return i;
        }
        return -1;
    }

    private sealed class CryptoDetection
    {
        public required string Algorithm { get; set; }
        public required string PatternType { get; set; }
        public required string Offset { get; set; }
        public required string Confidence { get; set; }
    }
}
