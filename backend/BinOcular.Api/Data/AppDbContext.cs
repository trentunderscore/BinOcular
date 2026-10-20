using BinOcular.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BinOcular.Api.Data;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<AnalysisRecord> Analyses => Set<AnalysisRecord>();
    public DbSet<AnalysisToolResult> ToolResults => Set<AnalysisToolResult>();
}