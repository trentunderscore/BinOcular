using System.Text;
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
    [RequestSizeLimit(104_857_600)] // 100 MB
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

        const long maxFileSize = 104_857_600; // 100 MB
        if (file.Length > maxFileSize)
        {
            return BadRequest(new
            {
                error = $"File exceeds the maximum allowed size of 100 MB for demo (received {(file.Length / (1024.0 * 1024.0)):F2} MB)."
            });
        }

        // Demo limit: Store only 1 file at a time. Clean up any previous analyses and their files on disk.
        var previousAnalyses = await dbContext.Analyses.ToListAsync(cancellationToken);
        foreach (var prev in previousAnalyses)
        {
            try
            {
                var oldPath = fileStorage.GetPath(prev.StoredFileName);
                if (System.IO.File.Exists(oldPath))
                {
                    System.IO.File.Delete(oldPath);
                }
            }
            catch
            {
                // Ignore deletion errors for missing files
            }
        }

        dbContext.Analyses.RemoveRange(previousAnalyses);

        var previousToolResults = await dbContext.ToolResults.ToListAsync(cancellationToken);
        dbContext.ToolResults.RemoveRange(previousToolResults);

        await dbContext.SaveChangesAsync(cancellationToken);

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


    [HttpGet]
    public async Task<IActionResult> GetAnalyses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = dbContext.Analyses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(a => a.OriginalFileName.ToLower().Contains(searchLower)
                                  || (a.Sha256 != null && a.Sha256.ToLower().Contains(searchLower)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                analysisId = a.Id,
                fileName = a.OriginalFileName,
                status = a.Status,
                sha256 = a.Sha256,
                createdAtUtc = a.CreatedAtUtc,
                startedAtUtc = a.StartedAtUtc,
                completedAtUtc = a.CompletedAtUtc,
                error = a.Error
            })
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Ok(new
        {
            items,
            page,
            pageSize,
            totalCount,
            totalPages
        });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken = default)
    {
        var total = await dbContext.Analyses.CountAsync(cancellationToken);
        var completed = await dbContext.Analyses.CountAsync(a => a.Status == "Completed", cancellationToken);
        var running = await dbContext.Analyses.CountAsync(a => a.Status == "Running", cancellationToken);
        var queued = await dbContext.Analyses.CountAsync(a => a.Status == "Queued", cancellationToken);
        var failed = await dbContext.Analyses.CountAsync(a => a.Status == "Failed", cancellationToken);

        return Ok(new
        {
            total,
            completed,
            running,
            queued,
            failed
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

    [HttpGet("{id:guid}/tool-results")]
    public async Task<IActionResult> GetToolResults(
        Guid id,
        CancellationToken cancellationToken)
    {
        var toolResults = await dbContext.ToolResults
            .AsNoTracking()
            .Where(tr => tr.AnalysisRecordId == id)
            .ToListAsync(cancellationToken);

        return Ok(toolResults);
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

    [HttpGet("{id:guid}/strings")]
    public async Task<IActionResult> GetStrings(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] int minLength = 4,
        [FromQuery] string? search = null,
        [FromQuery] string? encoding = null,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 1000);
        page = Math.Max(1, page);
        minLength = Math.Clamp(minLength, 3, 64);

        var analysis = await dbContext.Analyses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (analysis is null) return NotFound();

        var filePath = fileStorage.GetPath(analysis.StoredFileName);
        List<StringExtractor.StringItem> allItems;

        if (System.IO.File.Exists(filePath))
        {
            var bytes = await System.IO.File.ReadAllBytesAsync(filePath, cancellationToken);
            allItems = StringExtractor.Extract(bytes, minLength: minLength, maxStrings: 50000);
        }
        else
        {
            // Fallback: check if ToolResults has StringsTool
            var toolResult = await dbContext.ToolResults
                .AsNoTracking()
                .FirstOrDefaultAsync(tr => tr.AnalysisRecordId == id && tr.ToolName.Contains("String"), cancellationToken);

            if (toolResult != null && !string.IsNullOrWhiteSpace(toolResult.StandardOutput))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(toolResult.StandardOutput);
                    if (doc.RootElement.TryGetProperty("allStrings", out var arr))
                    {
                        allItems = System.Text.Json.JsonSerializer.Deserialize<List<StringExtractor.StringItem>>(arr.GetRawText()) ?? [];
                    }
                    else if (doc.RootElement.TryGetProperty("sampleStrings", out var samples))
                    {
                        var sList = System.Text.Json.JsonSerializer.Deserialize<List<string>>(samples.GetRawText()) ?? [];
                        allItems = sList.Select((s, idx) => new StringExtractor.StringItem { Offset = idx, Length = s.Length, Value = s }).ToList();
                    }
                    else
                    {
                        allItems = [];
                    }
                }
                catch
                {
                    allItems = [];
                }
            }
            else
            {
                allItems = [];
            }
        }

        // Apply filters
        var query = allItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(encoding) && !encoding.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(s => s.Encoding.Equals(encoding, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (category.Equals("ioc", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => !string.IsNullOrEmpty(s.Category));
            }
            else
            {
                query = query.Where(s => s.Category != null && s.Category.Contains(category, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLowerInvariant();
            query = query.Where(s => s.Value.ToLowerInvariant().Contains(searchLower) || s.OffsetHex.ToLowerInvariant().Contains(searchLower));
        }

        var filteredList = query.ToList();
        var totalCount = filteredList.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var items = filteredList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Ok(new
        {
            totalCount,
            page,
            pageSize,
            totalPages,
            items
        });
    }

    [HttpGet("{id:guid}/strings/download")]
    public async Task<IActionResult> DownloadStrings(
        Guid id,
        [FromQuery] int minLength = 4,
        [FromQuery] string? encoding = null,
        CancellationToken cancellationToken = default)
    {
        var analysis = await dbContext.Analyses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (analysis is null) return NotFound();

        var filePath = fileStorage.GetPath(analysis.StoredFileName);
        if (!System.IO.File.Exists(filePath)) return NotFound("Binary file not found on disk.");

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath, cancellationToken);
        var items = StringExtractor.Extract(bytes, minLength: Math.Clamp(minLength, 3, 64), maxStrings: 50000);

        if (!string.IsNullOrWhiteSpace(encoding) && !encoding.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            items = items.Where(s => s.Encoding.Equals(encoding, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# BinOcular Extracted Strings for: {analysis.OriginalFileName}");
        sb.AppendLine($"# Total: {items.Count} strings extracted | Generated: {DateTime.UtcNow:u}");
        sb.AppendLine("# Offset\tEncoding\tType\tValue");
        sb.AppendLine("# --------------------------------------------------");
        foreach (var item in items)
        {
            var cat = string.IsNullOrEmpty(item.Category) ? "Normal" : item.Category;
            sb.AppendLine($"{item.OffsetHex}\t{item.Encoding}\t{cat}\t{item.Value}");
        }

        var downloadBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var downloadName = $"{Path.GetFileNameWithoutExtension(analysis.OriginalFileName)}_strings.txt";
        return File(downloadBytes, "text/plain", downloadName);
    }
}