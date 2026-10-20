
using BinOcular.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BinOcular.Api.Controllers;

[ApiController]
[Route("api/analyses")]
public sealed class ToolResultController(
    AppDbContext dbContext) : ControllerBase
{
    [HttpGet("{analysisId:guid}/tool-results")]
    public async Task<IActionResult> GetToolResultsAsync(
        Guid analysisId,
        CancellationToken cancellationToken)
    {
        var toolResults = await dbContext.ToolResults
            .AsNoTracking()
            .Where(tr => tr.AnalysisRecordId == analysisId)
            .ToListAsync(cancellationToken);

        return Ok(toolResults);
    }
}
