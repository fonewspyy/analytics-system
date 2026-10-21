using Analytics.Application.Common;
using Analytics.Application.Users;
using Analytics.Domain;
using Analytics.Domain.Entities;
using Dapper;
using Npgsql;

namespace Analytics.Infrastructure.Persistence;

// Every write also inserts its audit row into system_logs inside the same statement
// (data-modifying CTE), so the change and its log commit or roll back together.
internal sealed class UserRepository(NpgsqlDataSource db) : IUserRepository
{
    private const string Columns = "id, email, full_name, role::text AS role, is_active, created_at, updated_at";

    private sealed class Row
    {
        public long Id { get; init; }
        public string Email { get; init; } = "";
        public string FullName { get; init; } = "";
        public string Role { get; init; } = "";
        public bool IsActive { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }

        public User ToEntity() => new()
        {
            Id = Id, Email = Email, FullName = FullName, Role = DbEnum.Parse<UserRole>(Role),
            IsActive = IsActive, CreatedAt = Time.Utc(CreatedAt), UpdatedAt = Time.Utc(UpdatedAt),
        };
    }

    public Task<PagedResult<User>> ListAsync(UserFilter filter, PageRequest page, CancellationToken ct)
    {
        var where = new Where()
            .Add(filter.Role is not null, "role = @role::user_role", "role", filter.Role is { } r ? DbEnum.ToDb(r) : null)
            .Add(filter.IsActive is not null, "is_active = @isActive", "isActive", filter.IsActive);
        return Paged.QueryAsync<Row, User>(db, Columns, "users", where, "id", page, r => r.ToEntity(), ct);
    }

    public async Task<User?> GetAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            $"SELECT {Columns} FROM users WHERE id = @id", new { id }, cancellationToken: ct));
        return row?.ToEntity();
    }

    public async Task<User> CreateAsync(User user, CancellationToken ct)
    {
        const string sql = $"""
            WITH ins AS (
                INSERT INTO users (email, full_name, role, is_active)
                VALUES (@Email, @FullName, @Role::user_role, @IsActive)
                RETURNING *
            ), audit AS (
                INSERT INTO system_logs (action, entity_type, entity_id, payload)
                SELECT 'user.created', 'user', id, to_jsonb(ins) FROM ins
            )
            SELECT {Columns} FROM ins;
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleAsync<Row>(new CommandDefinition(sql, Params(user), cancellationToken: ct));
        return row.ToEntity();
    }

    public async Task<User> UpdateAsync(User user, CancellationToken ct)
    {
        const string sql = $"""
            WITH before AS (
                SELECT to_jsonb(u) AS j FROM users u WHERE id = @Id
            ), upd AS (
                UPDATE users
                SET email = @Email, full_name = @FullName, role = @Role::user_role, is_active = @IsActive
                WHERE id = @Id
                RETURNING *
            ), audit AS (
                INSERT INTO system_logs (action, entity_type, entity_id, payload)
                SELECT 'user.updated', 'user', id,
                       jsonb_build_object('before', (SELECT j FROM before), 'after', to_jsonb(upd))
                FROM upd
            )
            SELECT {Columns} FROM upd;
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(sql, Params(user), cancellationToken: ct));
        return row?.ToEntity() ?? throw new NotFoundException("User", user.Id);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        const string sql = """
            WITH del AS (
                DELETE FROM users WHERE id = @id RETURNING *
            ), audit AS (
                INSERT INTO system_logs (action, entity_type, entity_id, payload)
                SELECT 'user.deleted', 'user', id, to_jsonb(del) FROM del
            )
            SELECT count(*) FROM del;
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { id }, cancellationToken: ct)) > 0;
    }

    public async Task<int> CountOwnedProjectsAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM projects WHERE owner_id = @id", new { id }, cancellationToken: ct));
    }

    private static object Params(User u) => new
    {
        u.Id, u.Email, u.FullName, Role = DbEnum.ToDb(u.Role), u.IsActive,
    };
}
