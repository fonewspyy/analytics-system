using Analytics.Application.Analytics;
using Analytics.Domain;
using Dapper;
using Npgsql;

namespace Analytics.Infrastructure.Persistence;

/// <summary>
/// Reporting queries, written as plain SQL so each one reads top to bottom in one place.
/// "Open" everywhere means status IN ('todo', 'in_progress'); "overdue" means open and past due.
/// </summary>
internal sealed class AnalyticsQueries(NpgsqlDataSource db) : IAnalyticsQueries
{
    private sealed class ProjectSummary
    {
        public long ProjectId { get; init; }
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public string Status { get; init; } = "";
        public string OwnerName { get; init; } = "";
        public DateOnly? DueDate { get; init; }
        public int TotalTasks { get; init; }
        public int Todo { get; init; }
        public int InProgress { get; init; }
        public int Done { get; init; }
        public int Cancelled { get; init; }
        public int Overdue { get; init; }
        public decimal? ProgressPercent { get; init; }
        public decimal? AvgCompletionHours { get; init; }
        public decimal RemainingEstimateHours { get; init; }
    }

    public async Task<IReadOnlyList<ProjectSummaryRow>> ProjectSummaryAsync(ProjectStatus? status, CancellationToken ct)
    {
        const string sql = """
            SELECT p.id                                                         AS project_id,
                   p.code,
                   p.name,
                   p.status::text                                               AS status,
                   owner.full_name                                              AS owner_name,
                   p.due_date,
                   count(t.id)::int                                             AS total_tasks,
                   (count(t.id) FILTER (WHERE t.status = 'todo'))::int          AS todo,
                   (count(t.id) FILTER (WHERE t.status = 'in_progress'))::int   AS in_progress,
                   (count(t.id) FILTER (WHERE t.status = 'done'))::int          AS done,
                   (count(t.id) FILTER (WHERE t.status = 'cancelled'))::int     AS cancelled,
                   (count(t.id) FILTER (WHERE t.status IN ('todo', 'in_progress')
                                          AND t.due_date < current_date))::int  AS overdue,
                   round(100.0 * count(t.id) FILTER (WHERE t.status = 'done')
                         / nullif(count(t.id) FILTER (WHERE t.status <> 'cancelled'), 0), 1)
                                                                                AS progress_percent,
                   round((avg(extract(epoch FROM t.completed_at - t.created_at) / 3600)
                          FILTER (WHERE t.status = 'done'))::numeric, 1)        AS avg_completion_hours,
                   coalesce(sum(t.estimate_hours) FILTER (WHERE t.status IN ('todo', 'in_progress')), 0)
                                                                                AS remaining_estimate_hours
            FROM projects p
            JOIN users owner  ON owner.id = p.owner_id
            LEFT JOIN tasks t ON t.project_id = p.id
            WHERE (@status::project_status IS NULL OR p.status = @status::project_status)
            GROUP BY p.id, owner.full_name
            ORDER BY p.code
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<ProjectSummary>(new CommandDefinition(sql,
            new { status = status is { } s ? DbEnum.ToDb(s) : null }, cancellationToken: ct));
        return rows.Select(r => new ProjectSummaryRow(
            r.ProjectId, r.Code, r.Name, DbEnum.Parse<ProjectStatus>(r.Status), r.OwnerName, r.DueDate,
            r.TotalTasks, r.Todo, r.InProgress, r.Done, r.Cancelled, r.Overdue,
            r.ProgressPercent, r.AvgCompletionHours, r.RemainingEstimateHours)).ToList();
    }

    private sealed class Workload
    {
        public long UserId { get; init; }
        public string FullName { get; init; } = "";
        public string Role { get; init; } = "";
        public int OpenTasks { get; init; }
        public int OpenHighPriority { get; init; }
        public int Overdue { get; init; }
        public int DoneLast30Days { get; init; }
        public decimal OpenEstimateHours { get; init; }
        public int LoadRank { get; init; }
    }

    public async Task<IReadOnlyList<WorkloadRow>> WorkloadAsync(CancellationToken ct)
    {
        const string sql = """
            WITH per_user AS (
                SELECT u.id                                                                 AS user_id,
                       u.full_name,
                       u.role::text                                                         AS role,
                       (count(t.id) FILTER (WHERE t.status IN ('todo', 'in_progress')))::int AS open_tasks,
                       (count(t.id) FILTER (WHERE t.status IN ('todo', 'in_progress')
                                              AND t.priority IN ('high', 'critical')))::int AS open_high_priority,
                       (count(t.id) FILTER (WHERE t.status IN ('todo', 'in_progress')
                                              AND t.due_date < current_date))::int          AS overdue,
                       (count(t.id) FILTER (WHERE t.status = 'done'
                                              AND t.completed_at >= now() - interval '30 days'))::int
                                                                                            AS done_last_30_days,
                       coalesce(sum(t.estimate_hours) FILTER (WHERE t.status IN ('todo', 'in_progress')), 0)
                                                                                            AS open_estimate_hours
                FROM users u
                LEFT JOIN tasks t ON t.assignee_id = u.id
                WHERE u.is_active
                GROUP BY u.id
            )
            SELECT *,
                   (dense_rank() OVER (ORDER BY open_estimate_hours DESC, open_tasks DESC))::int AS load_rank
            FROM per_user
            ORDER BY load_rank, user_id
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<Workload>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.Select(r => new WorkloadRow(
            r.UserId, r.FullName, DbEnum.Parse<UserRole>(r.Role), r.OpenTasks, r.OpenHighPriority,
            r.Overdue, r.DoneLast30Days, r.OpenEstimateHours, r.LoadRank)).ToList();
    }

    private sealed class Trend
    {
        public DateOnly Day { get; init; }
        public int Created { get; init; }
        public int Completed { get; init; }
        public int NetOpenChange { get; init; }
    }

    public async Task<IReadOnlyList<TrendRow>> TrendAsync(int days, CancellationToken ct)
    {
        // generate_series gives every day a row, so quiet days show 0 instead of disappearing.
        const string sql = """
            WITH bounds AS (
                SELECT current_date - (@days - 1) AS first_day
            ), calendar AS (
                SELECT d::date AS day
                FROM bounds, generate_series(first_day, current_date, interval '1 day') AS d
            ), created AS (
                SELECT created_at::date AS day, count(*) AS n
                FROM tasks, bounds WHERE created_at >= first_day GROUP BY 1
            ), completed AS (
                SELECT completed_at::date AS day, count(*) AS n
                FROM tasks, bounds WHERE completed_at >= first_day GROUP BY 1
            )
            SELECT c.day,
                   coalesce(cr.n, 0)::int AS created,
                   coalesce(co.n, 0)::int AS completed,
                   (sum(coalesce(cr.n, 0) - coalesce(co.n, 0)) OVER (ORDER BY c.day))::int AS net_open_change
            FROM calendar c
            LEFT JOIN created   cr USING (day)
            LEFT JOIN completed co USING (day)
            ORDER BY c.day
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<Trend>(new CommandDefinition(sql, new { days }, cancellationToken: ct));
        return rows.Select(r => new TrendRow(r.Day, r.Created, r.Completed, r.NetOpenChange)).ToList();
    }
}
