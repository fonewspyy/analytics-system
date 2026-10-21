using Analytics.Application.Common;
using Analytics.Application.Tasks;
using Analytics.Domain;
using Analytics.Domain.Entities;
using Dapper;
using Npgsql;

namespace Analytics.Infrastructure.Persistence;

internal sealed class TaskRepository(NpgsqlDataSource db) : ITaskRepository
{
    private const string Columns =
        "id, project_id, assignee_id, title, description, status::text AS status, priority::text AS priority, " +
        "estimate_hours, due_date, completed_at, created_at, updated_at";

    private const string OverdueSql = "(status IN ('todo', 'in_progress') AND due_date < current_date)";

    private sealed class Row
    {
        public long Id { get; init; }
        public long ProjectId { get; init; }
        public long? AssigneeId { get; init; }
        public string Title { get; init; } = "";
        public string? Description { get; init; }
        public string Status { get; init; } = "";
        public string Priority { get; init; } = "";
        public decimal? EstimateHours { get; init; }
        public DateOnly? DueDate { get; init; }
        public DateTime? CompletedAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }

        public TaskItem ToEntity() => TaskItem.Load(new TaskItem
        {
            Id = Id, ProjectId = ProjectId, AssigneeId = AssigneeId, Title = Title, Description = Description,
            Priority = DbEnum.Parse<TaskPriority>(Priority), EstimateHours = EstimateHours, DueDate = DueDate,
            CreatedAt = Time.Utc(CreatedAt), UpdatedAt = Time.Utc(UpdatedAt),
        }, DbEnum.Parse<TaskItemStatus>(Status), Time.Utc(CompletedAt));
    }

    public Task<PagedResult<TaskItem>> ListAsync(TaskFilter filter, PageRequest page, CancellationToken ct)
    {
        var where = new Where()
            .Add(filter.ProjectId is not null, "project_id = @projectId", "projectId", filter.ProjectId)
            .Add(filter.AssigneeId is not null, "assignee_id = @assigneeId", "assigneeId", filter.AssigneeId)
            .Add(filter.Status is not null, "status = @status::task_status", "status", filter.Status is { } s ? DbEnum.ToDb(s) : null)
            .Add(filter.Priority is not null, "priority = @priority::task_priority", "priority", filter.Priority is { } p ? DbEnum.ToDb(p) : null)
            .Add(filter.Overdue == true, OverdueSql)
            .Add(filter.Overdue == false, $"NOT coalesce({OverdueSql}, false)");
        return Paged.QueryAsync<Row, TaskItem>(db, Columns, "tasks", where, "id", page, r => r.ToEntity(), ct);
    }

    public async Task<TaskItem?> GetAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            $"SELECT {Columns} FROM tasks WHERE id = @id", new { id }, cancellationToken: ct));
        return row?.ToEntity();
    }

    public async Task<TaskItem> CreateAsync(TaskItem task, CancellationToken ct)
    {
        const string sql = $"""
            WITH ins AS (
                INSERT INTO tasks (project_id, assignee_id, title, description, status, priority,
                                   estimate_hours, due_date, completed_at)
                VALUES (@ProjectId, @AssigneeId, @Title, @Description, @Status::task_status, @Priority::task_priority,
                        @EstimateHours, @DueDate, @CompletedAt)
                RETURNING *
            ), audit AS (
                INSERT INTO system_logs (action, entity_type, entity_id, user_id, payload)
                SELECT 'task.created', 'task', id, assignee_id, to_jsonb(ins) FROM ins
            )
            SELECT {Columns} FROM ins;
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleAsync<Row>(new CommandDefinition(sql, Params(task), cancellationToken: ct));
        return row.ToEntity();
    }

    public async Task<TaskItem> UpdateAsync(TaskItem task, CancellationToken ct)
    {
        const string sql = $"""
            WITH before AS (
                SELECT to_jsonb(t) AS j FROM tasks t WHERE id = @Id
            ), upd AS (
                UPDATE tasks
                SET assignee_id = @AssigneeId, title = @Title, description = @Description,
                    status = @Status::task_status, priority = @Priority::task_priority,
                    estimate_hours = @EstimateHours, due_date = @DueDate, completed_at = @CompletedAt
                WHERE id = @Id
                RETURNING *
            ), audit AS (
                INSERT INTO system_logs (action, entity_type, entity_id, user_id, payload)
                SELECT 'task.updated', 'task', id, assignee_id,
                       jsonb_build_object('before', (SELECT j FROM before), 'after', to_jsonb(upd))
                FROM upd
            )
            SELECT {Columns} FROM upd;
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(sql, Params(task), cancellationToken: ct));
        return row?.ToEntity() ?? throw new NotFoundException("Task", task.Id);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        const string sql = """
            WITH del AS (
                DELETE FROM tasks WHERE id = @id RETURNING *
            ), audit AS (
                INSERT INTO system_logs (action, entity_type, entity_id, payload)
                SELECT 'task.deleted', 'task', id, to_jsonb(del) FROM del
            )
            SELECT count(*) FROM del;
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { id }, cancellationToken: ct)) > 0;
    }

    private static object Params(TaskItem t) => new
    {
        t.Id, t.ProjectId, t.AssigneeId, t.Title, t.Description,
        Status = DbEnum.ToDb(t.Status), Priority = DbEnum.ToDb(t.Priority),
        t.EstimateHours, t.DueDate, t.CompletedAt,
    };
}
