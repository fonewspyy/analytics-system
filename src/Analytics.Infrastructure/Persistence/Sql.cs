using System.Data;
using Analytics.Application.Common;
using Dapper;
using Npgsql;

namespace Analytics.Infrastructure.Persistence;

/// <summary>Builds an AND-ed WHERE clause from optional filters, with parameters.</summary>
internal sealed class Where
{
    private readonly List<string> _clauses = [];
    public DynamicParameters Parameters { get; } = new();

    public Where Add(bool when, string clause, string? name = null, object? value = null)
    {
        if (!when) return this;
        _clauses.Add(clause);
        if (name is not null) Parameters.Add(name, value);
        return this;
    }

    public override string ToString() => _clauses.Count == 0 ? "" : "WHERE " + string.Join(" AND ", _clauses);
}

internal static class Paged
{
    /// <summary>Runs a count and a page query in one round trip.</summary>
    public static async Task<PagedResult<T>> QueryAsync<TRow, T>(
        NpgsqlDataSource db, string columns, string from, Where where, string orderBy,
        PageRequest page, Func<TRow, T> map, CancellationToken ct)
    {
        where.Parameters.Add("limit", page.Limit);
        where.Parameters.Add("offset", page.Offset);
        var sql = $"""
            SELECT count(*) FROM {from} {where};
            SELECT {columns} FROM {from} {where} ORDER BY {orderBy} LIMIT @limit OFFSET @offset;
            """;

        await using var conn = await db.OpenConnectionAsync(ct);
        await using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, where.Parameters, cancellationToken: ct));
        var total = await grid.ReadSingleAsync<long>();
        var rows = await grid.ReadAsync<TRow>();
        return new PagedResult<T>(rows.Select(map).ToList(), total, page.Limit, page.Offset);
    }
}

internal static class Time
{
    // Npgsql returns timestamptz as DateTime with Kind=Utc.
    public static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    public static DateTimeOffset? Utc(DateTime? value) => value is null ? null : Utc(value.Value);
}

/// <summary>Dapper cannot bind DateOnly on its own; Npgsql can, so pass it through.</summary>
internal sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value;
    }

    public override DateOnly Parse(object value) => value switch
    {
        DateOnly d => d,
        DateTime dt => DateOnly.FromDateTime(dt),
        _ => throw new DataException($"Cannot convert {value.GetType()} to DateOnly."),
    };
}
