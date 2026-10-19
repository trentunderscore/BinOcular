using BinOcular.Api.Analysis;
using BinOcular.Api.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace BinOcular.Api.Tools;

public sealed class Sha256Tool(ILogger<Sha256Tool> logger) : IAnalysisTool
{
    public string Name => "SHA256 Hash Tool";

    public bool IsCompatible(string filePath) => true;

    public async Task<ToolResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            logger.LogWarning("SHA256 analysis skipped because the file path was invalid: {FilePath}", filePath);

            return new ToolResult
            {
                Tool = Name,
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = $"File not found or inaccessible: '{filePath}'"
            };
        }

        logger.LogInformation("Starting SHA256 hash for file {FilePath}", filePath);

        try
        {
            await using var stream = File.OpenRead(filePath);

            var hash = await SHA256.HashDataAsync(
                stream,
                cancellationToken);

            var result = new ToolResult
            {
                Tool = Name,
                ExitCode = 0,
                StandardOutput = Convert.ToHexString(hash).ToLowerInvariant(),
                StandardError = string.Empty
            };

            logger.LogInformation(
                "SHA256 hash completed successfully for file {FilePath}",
                filePath);

            return result;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("SHA256 hash operation was cancelled for file {FilePath}", filePath);
            throw;
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "I/O error while hashing file {FilePath}", filePath);
            return new ToolResult
            {
                Tool = Name,
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = ex.Message
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, "Access denied while hashing file {FilePath}", filePath);
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