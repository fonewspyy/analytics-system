using System.Text.Json;
using Analytics.Application.Common;
using Analytics.Domain;
using Analytics.Domain.Entities;

namespace Analytics.Application.Logs;

public sealed record SystemLogDto(
    long Id, DateTimeOffset OccurredAt, LogSeverity Level, string Action, string? EntityType,
    long? EntityId, long? UserId, string? Message, JsonElement Payload)
{
    public static SystemLogDto From(SystemLog l) =>
        new(l.Id, l.OccurredAt, l.Level, l.Action, l.EntityType, l.EntityId, l.UserId, l.Message,
            JsonDocument.Parse(l.Payload).RootElement.Clone());
}

public sealed record SystemLogFilter(
    LogSeverity? Level, string? Action, string? EntityType, long? EntityId,
    DateTimeOffset? From, DateTimeOffset? To);

public interface ISystemLogRepository
{
    Task<PagedResult<SystemLog>> ListAsync(SystemLogFilter filter, PageRequest page, CancellationToken ct);
    Task WriteAsync(SystemLog entry, CancellationToken ct);
}
