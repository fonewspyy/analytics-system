using Analytics.Application.Common;
using Analytics.Application.Projects;
using Analytics.Domain;
using Analytics.Domain.Entities;
using Dapper;
using Npgsql;

namespace Analytics.Infrastructure.Persistence;

internal sealed class ProjectRepository(NpgsqlDataSource db) : IProjectRepository
{
    private const string Columns =
        "id, code, name, description, owner_id, status::text AS status, start_date, due_date, created_at, updated_at";

    private sealed class Row
    {
        public long Id { get; init; }
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public string? Description { get; init; }
        public long OwnerId { get; init; }
        public string Status { get; init; } = "";
        public DateOnly StartDate { get; init; }
        public DateOnly? DueDate { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }

        public Project ToEntity() => Project.Load(new Project
        {
            Id = Id, Code = Code, Name = Name, Description = Description, OwnerId = OwnerId,
            StartDate = StartDate, DueDate = DueDate,
            CreatedAt = Time.Utc(CreatedAt), UpdatedAt = Time.Utc(UpdatedAt),
        }, DbEnum.Parse<ProjectStatus>(Status));
    }

    public Task<PagedResult<Project>> ListAsync(ProjectFilter filter, PageRequest page, CancellationToken ct)
    {
        var where = new Where()
            .Add(filter.Status is not null, "status = @status::project_status", "status", filter.Status is { } s ? DbEnum.ToDb(s) : null)
            .Add(filter.OwnerId is not null, "owner_id = @ownerId", "ownerId", filter.OwnerId);
        return Paged.QueryAsync<Row, Project>(db, Columns, "projects", where, "id", page, r => r.ToEntity(), ct);
    }

    public async Task<Project?> GetAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            $"SELECT {Columns} FROM projects WHERE id = @id", new { id }, cancellationToken: ct));
        return row?.ToEntity();
    }

    public async Task<Project> CreateAsync(Project project, CancellationToken ct)
    {
        const string sql = $"""
            WITH ins AS (
                INSERT INTO projects (code, name, description, owner_id, status, start_date, due_date)
                VALUES (@Code, @Name, @Description, @OwnerId, @Status::project_status, @StartDate, @DueDate)
                RETURNING *
            ), audit AS (
                INSERT INTO system_logs (action, entity_type, entity_id, payload)
                SELECT 'project.created', 'project', id, to_jsonb(ins) FROM ins
            )
            SELECT {Columns} FROM ins;
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleAsync<Row>(new CommandDefinition(sql, Params(project), cancellationToken: ct));
        return row.ToEntity();
    }

    public async Task<Project> UpdateAsync(Project project, CancellationToken ct)
    {
        const string sql = $"""
            WITH before AS (
                SELECT to_jsonb(p) AS j FROM projects p WHERE id = @Id
            ), upd AS (
                UPDATE projects
                SET name = @Name, description = @Description, owner_id = @OwnerId,
                    status = @Status::project_status, start_date = @StartDate, due_date = @DueDate
                WHERE id = @Id
                RETURNING *
            ), audit AS (
                INSERT INTO system_logs (action, entity_type, entity_id, payload)
                SELECT 'project.updated', 'project', id,
                       jsonb_build_object('before', (SELECT j FROM before), 'after', to_jsonb(upd))
                FROM upd
            )
            SELECT {Columns} FROM upd;
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(sql, Params(project), cancellationToken: ct));
        return row?.ToEntity() ?? throw new NotFoundException("Project", project.Id);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        // Tasks go with the project (ON DELETE CASCADE); the audit row records how many.
        const string sql = """
            WITH task_count AS (
                SELECT count(*) AS n FROM tasks WHERE project_id = @id
            ), del AS (
                DELETE FROM projects WHERE id = @id RETURNING *
            ), audit AS (
                INSERT INTO system_logs (action, entity_type, entity_id, payload)
                SELECT 'project.deleted', 'project', id,
                       to_jsonb(del) || jsonb_build_object('deleted_tasks', (SELECT n FROM task_count))
                FROM del
            )
            SELECT count(*) FROM del;
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { id }, cancellationToken: ct)) > 0;
    }

    public async Task<(int Total, int Open)> CountTasksAsync(long id, CancellationToken ct)
    {
        const string sql = """
            SELECT count(*)::int AS total,
                   (count(*) FILTER (WHERE status IN ('todo', 'in_progress')))::int AS open
            FROM tasks WHERE project_id = @id
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        var counts = await conn.QuerySingleAsync<(int, int)>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
        return counts;
    }

    private static object Params(Project p) => new
    {
        p.Id, p.Code, p.Name, p.Description, p.OwnerId, Status = DbEnum.ToDb(p.Status), p.StartDate, p.DueDate,
    };
}
