using BinOcular.Api.Analysis;
using BinOcular.Api.Models;
using Microsoft.Extensions.Logging;

namespace BinOcular.Api.Tools;

public sealed class DIETool(
    ToolRunner toolRunner,
    ILogger<DIETool> logger) : IAnalysisTool
{
    public string Name => "Dependency Walker (DIE) Tool";

    public bool IsCompatible(string filePath) => true;

    public async Task<ToolResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting DIE analysis for file {FilePath}", filePath);

        var result = await toolRunner.RunAsync(
            "diec",
            new[] { "-d", filePath },
            cancellationToken);

        if (result.ExitCode != 0)
        {
            logger.LogWarning(
                "DIE analysis completed with non-zero exit code {ExitCode} for file {FilePath}",
                result.ExitCode,
                filePath);
        }

        return result;
    }
}