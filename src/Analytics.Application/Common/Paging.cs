using System.Collections;

namespace Analytics.Application.Common;

public sealed record PageRequest
{
    public const int MaxLimit = 1000;

    public int Limit { get; }
    public int Offset { get; }

    public PageRequest(int? limit, int? offset)
    {
        Limit = Math.Clamp(limit ?? 50, 1, MaxLimit);
        Offset = Math.Max(offset ?? 0, 0);
    }
}

/// <summary>Lets the CSV formatter export the rows of any page without knowing T.</summary>
public interface IPagedResult
{
    IEnumerable Rows { get; }
    Type RowType { get; }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, long Total, int Limit, int Offset) : IPagedResult
{
    IEnumerable IPagedResult.Rows => Items;
    Type IPagedResult.RowType => typeof(T);
}
