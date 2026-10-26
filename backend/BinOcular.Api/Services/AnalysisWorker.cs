using BinOcular.Api.Analysis;
using BinOcular.Api.Data;
using BinOcular.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BinOcular.Api.Services;

public sealed class AnalysisWorker(
    AnalysisQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<AnalysisWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AnalysisWorker background service started.");

        await foreach (var analysisId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAnalysisAsync(analysisId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled failure while processing analysis {AnalysisId}", analysisId);
            }
        }

        logger.LogInformation("AnalysisWorker background service stopped.");
    }

    private async Task ProcessAnalysisAsync(
        Guid analysisId,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fileStorage = scope.ServiceProvider.GetRequiredService<AnalysisFileStorage>();
        var tools = scope.ServiceProvider.GetServices<IAnalysisTool>().ToList();
        var formatters = scope.ServiceProvider.GetServices<IToolResultFormatter>().ToList();

        var analysis = await dbContext.Analyses
            .FirstOrDefaultAsync(a => a.Id == analysisId, cancellationToken);

        if (analysis is null)
        {
            logger.LogWarning("Analysis {AnalysisId} was not found in database", analysisId);
            return;
        }

        analysis.Status = "Running";
        analysis.StartedAtUtc = DateTimeOffset.UtcNow;
        analysis.Error = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        var filePath = fileStorage.GetPath(analysis.StoredFileName);

        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Uploaded binary file could not be found on disk.", filePath);
            }

            var compatibleTools = tools.Where(tool => tool.IsCompatible(filePath)).ToList();
            logger.LogInformation("Executing {ToolCount} compatible tools for analysis {AnalysisId}", compatibleTools.Count, analysisId);

            foreach (var tool in compatibleTools)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ToolResult result;
                try
                {
                    result = await tool.AnalyzeAsync(filePath, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception toolEx)
                {
                    logger.LogError(toolEx, "Tool '{ToolName}' threw an unexpected exception during analysis of {FilePath}", tool.Name, filePath);
                    result = new ToolResult
                    {
                        Tool = tool.Name,
                        ExitCode = -1,
                        StandardOutput = string.Empty,
                        StandardError = $"Tool execution error: {toolEx.Message}"
                    };
                }

                var toolResult = new AnalysisToolResult
                {
                    AnalysisRecordId = analysis.Id,
                    ToolName = string.IsNullOrWhiteSpace(tool.Name) ? result.Tool : tool.Name,
                    ExitCode = result.ExitCode,
                    StandardOutput = result.StandardOutput,
                    StandardError = result.StandardError
                };

                // Match formatter safely
                var formatter = formatters.FirstOrDefault(f =>
                    string.Equals(f.ToolName, tool.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(f.ToolName, result.Tool, StringComparison.OrdinalIgnoreCase));

                if (formatter is not null)
                {
                    try
                    {
                        var formattedOutput = formatter.Format(result);
                        toolResult.FormattedOutputJson = JsonSerializer.Serialize(formattedOutput, JsonOptions);
                    }
                    catch (Exception fmtEx)
                    {
                        logger.LogWarning(fmtEx, "Could not format output for tool '{ToolName}'", tool.Name);
                    }
                }

                dbContext.ToolResults.Add(toolResult);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            analysis.Status = "Completed";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            analysis.Status = "Queued";
            analysis.StartedAtUtc = null;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Analysis {AnalysisId} failed fatally", analysis.Id);
            analysis.Status = "Failed";
            analysis.Error = ex.Message;
        }
        finally
        {
            analysis.CompletedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        logger.LogInformation("Analysis {AnalysisId} finished with status '{Status}'", analysis.Id, analysis.Status);
    }
}
