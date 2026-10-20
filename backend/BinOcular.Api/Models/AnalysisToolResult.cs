
namespace BinOcular.Api.Models;

public sealed class AnalysisToolResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AnalysisRecordId { get; set; }

    public required string ToolName { get; set; }

    public int ExitCode { get; set; }

    public string? StandardOutput { get; set; }

    public string? StandardError { get; set; }

    public DateTimeOffset CompletedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;
}
