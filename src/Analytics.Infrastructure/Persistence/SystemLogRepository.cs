using Analytics.Application.Common;
using Analytics.Application.Logs;
using Analytics.Domain;
using Analytics.Domain.Entities;
using Dapper;
using Npgsql;

namespace Analytics.Infrastructure.Persistence;

internal sealed class SystemLogRepository(NpgsqlDataSource db) : ISystemLogRepository
{
    private const string Columns =
        "id, occurred_at, level::text AS level, action, entity_type, entity_id, user_id, message, payload::text AS payload";

    private sealed class Row
    {
        public long Id { get; init; }
        public DateTime OccurredAt { get; init; }
        public string Level { get; init; } = "";
        public string Action { get; init; } = "";
        public string? EntityType { get; init; }
        public long? EntityId { get; init; }
        public long? UserId { get; init; }
        public string? Message { get; init; }
        public string Payload { get; init; } = "{}";

        public SystemLog ToEntity() => new()
        {
            Id = Id, OccurredAt = Time.Utc(OccurredAt), Level = DbEnum.Parse<LogSeverity>(Level), Action = Action,
            EntityType = EntityType, EntityId = EntityId, UserId = UserId, Message = Message, Payload = Payload,
        };
    }

    public Task<PagedResult<SystemLog>> ListAsync(SystemLogFilter f, PageRequest page, CancellationToken ct)
    {
        var where = new Where()
            .Add(f.Level is not null, "level = @level::log_level", "level", f.Level is { } l ? DbEnum.ToDb(l) : null)
            .Add(f.Action is not null, "action = @action", "action", f.Action)
            .Add(f.EntityType is not null, "entity_type = @entityType", "entityType", f.EntityType)
            .Add(f.EntityId is not null, "entity_id = @entityId", "entityId", f.EntityId)
            .Add(f.From is not null, "occurred_at >= @from", "from", f.From)
            .Add(f.To is not null, "occurred_at < @to", "to", f.To);
        return Paged.QueryAsync<Row, SystemLog>(db, Columns, "system_logs", where, "occurred_at DESC, id DESC", page, r => r.ToEntity(), ct);
    }

    public async Task WriteAsync(SystemLog e, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO system_logs (level, action, entity_type, entity_id, user_id, message, payload)
            VALUES (@Level::log_level, @Action, @EntityType, @EntityId, @UserId, @Message, @Payload::jsonb)
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Level = DbEnum.ToDb(e.Level), e.Action, e.EntityType, e.EntityId, e.UserId, e.Message, e.Payload,
        }, cancellationToken: ct));
    }
}
