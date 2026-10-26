using BinOcular.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BinOcular.Api.Data;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<AnalysisRecord> Analyses => Set<AnalysisRecord>();
    public DbSet<AnalysisToolResult> ToolResults => Set<AnalysisToolResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var converter = new DateTimeOffsetToStringConverter();

        modelBuilder.Entity<AnalysisRecord>(entity =>
        {
            entity.Property(a => a.CreatedAtUtc)
                  .HasConversion(converter);

            entity.Property(a => a.StartedAtUtc)
                  .HasConversion(converter);

            entity.Property(a => a.CompletedAtUtc)
                  .HasConversion(converter);

            entity.HasIndex(a => a.CreatedAtUtc);
            entity.HasIndex(a => a.Status);
        });

        modelBuilder.Entity<AnalysisToolResult>(entity =>
        {
            entity.Property(tr => tr.CompletedAtUtc)
                  .HasConversion(converter);

            entity.HasIndex(tr => tr.AnalysisRecordId);
        });
    }
}