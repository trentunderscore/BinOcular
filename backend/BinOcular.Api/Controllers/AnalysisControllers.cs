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
    AnalysisFileStorage fileStorage) : ControllerBase
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

        var analysis = new AnalysisRecord
        {
            OriginalFileName = originalFileName,
            StoredFileName = storedFileName,
            Status = "Queued"
        };

        try
        {
            dbContext.Analyses.Add(analysis);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            var savedPath = fileStorage.GetPath(storedFileName);

            if (System.IO.File.Exists(savedPath))
                System.IO.File.Delete(savedPath);

            throw;
        }

        return Accepted(new
        {
            analysisId = analysis.Id,
            fileName = analysis.OriginalFileName,
            status = analysis.Status,
            createdAtUtc = analysis.CreatedAtUtc
        });
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
}