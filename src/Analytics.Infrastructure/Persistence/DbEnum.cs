using System.Collections.Concurrent;
using System.Text.Json;

namespace Analytics.Infrastructure.Persistence;

/// <summary>
/// C# enum &lt;-&gt; PostgreSQL enum label. Members are PascalCase, labels snake_case
/// (InProgress &lt;-&gt; in_progress) — the same policy the JSON API uses, so a value
/// reads the same in the database, the API and CSV exports.
/// </summary>
public static class DbEnum
{
    private static readonly ConcurrentDictionary<Type, Dictionary<string, object>> Lookup = new();

    public static string ToDb(Enum value) => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    public static T Parse<T>(string label) where T : struct, Enum
    {
        var map = Lookup.GetOrAdd(typeof(T), _ =>
            Enum.GetValues<T>().ToDictionary(v => ToDb(v), v => (object)v));
        return map.TryGetValue(label, out var value)
            ? (T)value
            : throw new InvalidOperationException($"Database label '{label}' has no {typeof(T).Name} member.");
    }
}
