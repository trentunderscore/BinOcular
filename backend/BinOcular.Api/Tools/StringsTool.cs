using System.Text.Json;
using BinOcular.Api.Analysis;
using BinOcular.Api.Models;
using BinOcular.Api.Services;

namespace BinOcular.Api.Tools;

public sealed class StringsTool(ILogger<StringsTool> logger) : IAnalysisTool
{
    public string Name => "String Analysis & IOC Extraction";

    public bool IsCompatible(string filePath) => File.Exists(filePath);

    public async Task<ToolResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Extracting printable strings and IOCs from {FilePath}", filePath);

        try
        {
            var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);

            var extractedItems = StringExtractor.Extract(fileBytes, minLength: 4, maxStrings: 10000);
            var iocMatches = extractedItems.Where(s => !string.IsNullOrEmpty(s.Category)).ToList();

            var result = new
            {
                totalExtractedCount = extractedItems.Count,
                suspiciousIocCount = iocMatches.Count,
                iocs = iocMatches.Take(200).Select(s => new
                {
                    Category = s.Category!,
                    Value = s.Value,
                    Severity = s.Severity ?? "Medium"
                }).ToList(),
                allStrings = extractedItems.Take(2000).Select(s => new
                {
                    offset = s.Offset,
                    offsetHex = s.OffsetHex,
                    length = s.Length,
                    encoding = s.Encoding,
                    value = s.Value,
                    category = s.Category,
                    severity = s.Severity
                }).ToList(),
                sampleStrings = extractedItems
                    .Where(s => s.Length >= 6 && !s.Value.All(char.IsDigit))
                    .Take(100)
                    .Select(s => s.Value)
                    .ToList()
            };

            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });

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
            logger.LogError(ex, "StringsTool failed on {FilePath}", filePath);
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
