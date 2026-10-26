using System.Text;
using System.Text.Json;
using BinOcular.Api.Analysis;
using BinOcular.Api.Models;

namespace BinOcular.Api.Tools;

public sealed class ImportsExportsTool(ILogger<ImportsExportsTool> logger) : IAnalysisTool
{
    public string Name => "Imports & Library Dependencies";

    public bool IsCompatible(string filePath) => File.Exists(filePath);

    public async Task<ToolResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Inspecting imports and library dependencies for {FilePath}", filePath);

        try
        {
            await using var stream = File.OpenRead(filePath);
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var fileBytes = new byte[Math.Min(10_000_000, stream.Length)];
            _ = await stream.ReadAsync(fileBytes.AsMemory(0, fileBytes.Length), cancellationToken);

            // Scan for common DLL dependencies in binary
            string[] knownDlls =
            [
                "KERNEL32.dll", "USER32.dll", "GDI32.dll", "ADVAPI32.dll", "SHELL32.dll",
                "WS2_32.dll", "WININET.dll", "URLMON.dll", "CRYPT32.dll", "NTDLL.dll",
                "MSVCRT.dll", "OLE32.dll", "OLEAUT32.dll", "COMCTL32.dll", "SHLWAPI.dll",
                "IPHLPAPI.dll", "WINHTTP.dll", "PSAPI.dll", "NETAPI32.dll", "WLDAP32.dll",
                "libc.so", "libm.so", "libpthread.so", "libdl.so", "libcrypto.so", "libssl.so"
            ];

            var binaryContent = Encoding.ASCII.GetString(fileBytes);

            foreach (var dll in knownDlls)
            {
                if (binaryContent.Contains(dll, StringComparison.OrdinalIgnoreCase))
                {
                    libraries.Add(dll);

                    if (dll.Equals("WS2_32.dll", StringComparison.OrdinalIgnoreCase) ||
                        dll.Equals("WININET.dll", StringComparison.OrdinalIgnoreCase) ||
                        dll.Equals("WINHTTP.dll", StringComparison.OrdinalIgnoreCase) ||
                        dll.Equals("URLMON.dll", StringComparison.OrdinalIgnoreCase) ||
                        dll.Equals("IPHLPAPI.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        capabilities.Add("Network Communication / Sockets");
                    }

                    if (dll.Equals("ADVAPI32.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        capabilities.Add("Windows Registry & Security Tokens");
                    }

                    if (dll.Equals("CRYPT32.dll", StringComparison.OrdinalIgnoreCase) ||
                        dll.Equals("libcrypto.so", StringComparison.OrdinalIgnoreCase) ||
                        dll.Equals("libssl.so", StringComparison.OrdinalIgnoreCase))
                    {
                        capabilities.Add("Cryptographic Operations / SSL");
                    }

                    if (dll.Equals("PSAPI.dll", StringComparison.OrdinalIgnoreCase) ||
                        dll.Equals("NTDLL.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        capabilities.Add("Process & Thread Enumeration");
                    }

                    if (dll.Equals("SHELL32.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        capabilities.Add("Shell Commands & File Operations");
                    }
                }
            }

            var result = new
            {
                totalDependencies = libraries.Count,
                libraries = libraries.OrderBy(l => l).ToList(),
                detectedCapabilities = capabilities.OrderBy(c => c).ToList()
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
            logger.LogError(ex, "ImportsExportsTool failed on {FilePath}", filePath);
            return new ToolResult
            {
                Tool = Name,
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = ex.Message
            };
        }
    }
}
