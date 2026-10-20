
using BinOcular.Api.Data;
using BinOcular.Api.Models;
using BinOcular.Api.Analysis;
using Microsoft.EntityFrameworkCore;

namespace BinOcular.Api.Services;

public sealed class AnalysisWorker(
    AnalysisQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<AnalysisWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await foreach (var analysisId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAnalysisAsync(analysisId, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to process analysis {AnalysisId}",
                    analysisId);
            }
        }
    }

    private async Task ProcessAnalysisAsync(
        Guid analysisId,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var fileStorage = scope.ServiceProvider
            .GetRequiredService<AnalysisFileStorage>();

        var tools = scope.ServiceProvider
            .GetServices<IAnalysisTool>();

        var analysis = await dbContext.Analyses
            .FirstOrDefaultAsync(
                a => a.Id == analysisId,
                cancellationToken);

        if (analysis is null)
        {
            logger.LogWarning(
                "Analysis {AnalysisId} was not found",
                analysisId);
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
                throw new FileNotFoundException(
                    "The uploaded file could not be found.",
                    filePath);
            }

            var compatibleTools = tools
                .Where(tool => tool.IsCompatible(filePath))
                .ToList();

            foreach (var tool in compatibleTools)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await tool.AnalyzeAsync(
                    filePath,
                    cancellationToken);

                dbContext.ToolResults.Add(new AnalysisToolResult
                {
                    AnalysisRecordId = analysis.Id,
                    ToolName = result.Tool,
                    ExitCode = result.ExitCode,
                    StandardOutput = result.StandardOutput,
                    StandardError = result.StandardError
                });

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            analysis.Status = "Completed";
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            analysis.Status = "Queued";
            analysis.StartedAtUtc = null;

            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Analysis {AnalysisId} failed",
                analysis.Id);

            analysis.Status = "Failed";
            analysis.Error = ex.Message;
        }
        finally
        {
            analysis.CompletedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        logger.LogInformation(
            "Analysis {AnalysisId} finished with status {Status}",
            analysis.Id,
            analysis.Status);
    }
}
