using FinanceFoodTracker.Domain.Enums;

namespace FinanceFoodTracker.Domain.Entities;

public class Statement : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string PdfPath { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public string? ParsedOperations { get; set; }
    public string? AiRawResponse { get; set; }
    public string? Error { get; set; }
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;
    public DateTimeOffset? ConfirmedAt { get; set; }
    public int CreatedCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
