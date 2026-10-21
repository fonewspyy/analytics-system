namespace Analytics.Domain.Entities;

public sealed class SystemLog
{
    public long Id { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public LogSeverity Level { get; init; } = LogSeverity.Info;
    public required string Action { get; init; }
    public string? EntityType { get; init; }
    public long? EntityId { get; init; }
    public long? UserId { get; init; }
    public string? Message { get; init; }
    /// <summary>Raw JSON (stored as JSONB).</summary>
    public string Payload { get; init; } = "{}";
}
