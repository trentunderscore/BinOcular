namespace BinOcular.Api.Models;

public sealed class AnalysisRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string OriginalFileName { get; set; }

    public required string StoredFileName { get; set; }

    public string Status { get; set; } = "Queued";

    public string? Sha256 { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? Error { get; set; }
}