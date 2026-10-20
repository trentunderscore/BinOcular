
using BinOcular.Api.Data;
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
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
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

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Started analysis {AnalysisId} for {FileName}",
            analysis.Id,
            analysis.OriginalFileName);

        // Actual analysis tools will be executed in the next step.

        analysis.Status = "Completed";
        analysis.CompletedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Completed analysis {AnalysisId}",
            analysis.Id);
    }
}
