using System.Text;
using System.Text.Json;
using BinOcular.Api.Analysis;
using BinOcular.Api.Models;

namespace BinOcular.Api.Tools;

public sealed class CompilerAndTimestampTool(ILogger<CompilerAndTimestampTool> logger) : IAnalysisTool
{
    public string Name => "Build Environment & Compiler Fingerprinter";

    public bool IsCompatible(string filePath) => File.Exists(filePath);

    public async Task<ToolResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Inspecting build timestamp, compiler fingerprint, and tooling for {FilePath}", filePath);

        try
        {
            var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var fileText = Encoding.ASCII.GetString(fileBytes);

            var timestampStr = "Unknown";
            var timestampAnomaly = "None";
            var detectedCompiler = "Unknown Compiler / Native Assembler";
            var detectedFramework = "Native C/C++";
            var richHeaderFound = false;

            // 1. PE Header Inspection
            if (fileBytes.Length >= 0x40 && fileBytes[0] == 0x4D && fileBytes[1] == 0x5A) // MZ
            {
                var peOffset = BitConverter.ToInt32(fileBytes, 0x3C);
                if (peOffset > 0 && peOffset + 24 <= fileBytes.Length)
                {
                    // Check Rich Header between 0x80 and peOffset
                    for (var r = 0x80; r + 4 < peOffset; r += 4)
                    {
                        if (fileBytes[r] == 0x52 && fileBytes[r + 1] == 0x69 && fileBytes[r + 2] == 0x63 && fileBytes[r + 3] == 0x68) // "Rich"
                        {
                            richHeaderFound = true;
                            detectedCompiler = "Microsoft Visual C/C++ (MSVC)";
                            break;
                        }
                    }

                    // COFF TimeDateStamp at peOffset + 8
                    var rawTimestamp = BitConverter.ToUInt32(fileBytes, peOffset + 8);
                    if (rawTimestamp == 0)
                    {
                        timestampStr = "0 (Deliberately Cleared / Zeroed)";
                        timestampAnomaly = "Timestamp Zeroed (Common in Go/Reproducible Builds or Anti-Forensics)";
                    }
                    else
                    {
                        try
                        {
                            var buildDate = DateTimeOffset.FromUnixTimeSeconds(rawTimestamp);
                            timestampStr = buildDate.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

                            if (buildDate > DateTimeOffset.UtcNow.AddDays(1))
                            {
                                timestampAnomaly = $"Future Timestamp: {buildDate:yyyy-MM-dd} (Time-Stomping / Anti-Forensics)";
                            }
                            else if (buildDate.Year < 1998)
                            {
                                timestampAnomaly = $"Historical Timestamp: {buildDate.Year} (Potential Timestamp Manipulation)";
                            }
                        }
                        catch
                        {
                            timestampStr = $"Invalid Epoch ({rawTimestamp})";
                            timestampAnomaly = "Corrupt Timestamp";
                        }
                    }
                }
            }

            // 2. Language & Framework Fingerprints
            if (fileText.Contains("mscoree.dll") || fileText.Contains("_CorExeMain") || fileText.Contains(".NETFramework"))
            {
                detectedCompiler = ".NET CLR / Managed C#";
                detectedFramework = ".NET Runtime (Managed Executable)";
            }
            else if (fileText.Contains("runtime.main") || fileText.Contains("go.buildid"))
            {
                detectedCompiler = "Go Compiler (gc)";
                detectedFramework = "Golang Static Runtime";
            }
            else if (fileText.Contains("rustc/") || fileText.Contains("/rustc-"))
            {
                detectedCompiler = "Rust Compiler (rustc / LLVM)";
                detectedFramework = "Rust Standard Library";
            }
            else if (fileText.Contains("__mingw_") || fileText.Contains("GCC: (GNU)"))
            {
                detectedCompiler = "GCC / MinGW (GNU Compiler Collection)";
                detectedFramework = "MinGW CRT Runtime";
            }
            else if (fileText.Contains("Py_Initialize") || fileText.Contains("pyi-runtime"))
            {
                detectedCompiler = "Python Packed (PyInstaller / Py2exe)";
                detectedFramework = "Embedded CPython Runtime";
            }
            else if (fileText.Contains("Borland") || fileText.Contains("Delphi"))
            {
                detectedCompiler = "Borland Delphi / C++ Builder";
                detectedFramework = "VCL Runtime";
            }

            var result = new
            {
                buildTimestamp = timestampStr,
                timestampAnomaly,
                compiler = detectedCompiler,
                framework = detectedFramework,
                hasRichHeader = richHeaderFound,
                richHeaderDetails = richHeaderFound ? "MSVC Rich Header Signature Validated" : "Not Present / Stripped"
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
            logger.LogError(ex, "CompilerAndTimestampTool failed on {FilePath}", filePath);
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
