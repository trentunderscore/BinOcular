using BinOcular.Api.Data;
using BinOcular.Api.Models;
using BinOcular.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BinOcular.Api.Controllers;

[ApiController]
[Route("api/analyses")]
public sealed class AnalysesController(
    AppDbContext dbContext,
    AnalysisFileStorage fileStorage,
    FileHashService fileHashService,
    AnalysisQueue queue) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> Upload(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new
            {
                error = "A non-empty file is required."
            });
        }

        var originalFileName = Path.GetFileName(file.FileName);

        string storedFileName;

        try
        {
            storedFileName = await fileStorage.SaveAsync(
                file,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "File could not be saved.");
        }

        var savedPath = fileStorage.GetPath(storedFileName);

        try
        {
            var sha256 = await fileHashService.ComputeSha256Async(
                savedPath,
                cancellationToken);

            var analysis = new AnalysisRecord
            {
                OriginalFileName = originalFileName,
                StoredFileName = storedFileName,
                Sha256 = sha256,
                Status = "Queued"
            };

            dbContext.Analyses.Add(analysis);
            await dbContext.SaveChangesAsync(cancellationToken);
            await queue.EnqueueAsync(
    analysis.Id,
    cancellationToken);

            return Accepted(new
            {
                analysisId = analysis.Id,
                fileName = analysis.OriginalFileName,
                status = analysis.Status,
                sha256 = analysis.Sha256,
                createdAtUtc = analysis.CreatedAtUtc
            });
        }
        catch
        {
            if (System.IO.File.Exists(savedPath))
                System.IO.File.Delete(savedPath);

            throw;
        }
    }


    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAnalysis(
        Guid id,
        CancellationToken cancellationToken)
    {
        var analysis = await dbContext.Analyses
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Id,
                a.OriginalFileName,
                a.Status,
                a.Sha256,
                a.CreatedAtUtc,
                a.StartedAtUtc,
                a.CompletedAtUtc,
                a.Error
            })
            .FirstOrDefaultAsync(cancellationToken);

        return analysis is null ? NotFound() : Ok(analysis);
    }

    // Delete an analysis and its associated file
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAnalysis(
        Guid id,
        CancellationToken cancellationToken)
    {
        var analysis = await dbContext.Analyses
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);  

        if (analysis is null)
        {
            return NotFound();
        }

        // Delete the associated file
        var filePath = fileStorage.GetPath(analysis.StoredFileName);
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }

        dbContext.Analyses.Remove(analysis);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }


    // Delete all analyses and their associated files
    [HttpDelete]
    public async Task<IActionResult> DeleteAllAnalyses(
        CancellationToken cancellationToken)
    {
        var analyses = await dbContext.Analyses.ToListAsync(cancellationToken); 

        foreach (var analysis in analyses)
        {
            // Delete the associated file
            var filePath = fileStorage.GetPath(analysis.StoredFileName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }

        dbContext.Analyses.RemoveRange(analyses);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Delete all tool results as well
        var toolResults = await dbContext.ToolResults.ToListAsync(cancellationToken);
        dbContext.ToolResults.RemoveRange(toolResults);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}