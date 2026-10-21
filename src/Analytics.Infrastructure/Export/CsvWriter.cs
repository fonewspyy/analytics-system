using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Analytics.Infrastructure.Persistence;

namespace Analytics.Infrastructure.Export;

/// <summary>
/// RFC 4180 CSV from any list of flat records: one column per public property,
/// snake_case headers, ISO dates, enum labels as in the API.
/// </summary>
public static class CsvWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static async Task WriteAsync(TextWriter writer, IEnumerable rows, Type rowType)
    {
        writer.NewLine = "\r\n";
        var props = rowType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();

        await writer.WriteLineAsync(string.Join(',', props.Select(p => Quote(JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name)))));
        foreach (var row in rows)
            await writer.WriteLineAsync(string.Join(',', props.Select(p => Quote(Format(p.GetValue(row))))));
    }

    public static string Format(object? value) => value switch
    {
        null => "",
        string s => GuardFormula(s),
        DateTimeOffset d => d.ToString("O", Inv),
        DateTime d => d.ToString("O", Inv),
        DateOnly d => d.ToString("yyyy-MM-dd", Inv),
        Enum e => DbEnum.ToDb(e),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, Inv),
        _ => value.ToString() ?? "",
    };

    public static string Quote(string field) =>
        field.AsSpan().IndexOfAny(",\"\r\n") >= 0
            ? "\"" + field.Replace("\"", "\"\"") + "\""
            : field;

    /// <summary>
    /// A text cell starting with = + - @ is run as a formula by Excel/Sheets (CSV injection).
    /// Prefixing a quote makes it plain text. Numbers are not strings, so they never get here.
    /// </summary>
    private static string GuardFormula(string s) =>
        s.Length > 0 && "=+-@\t\r".Contains(s[0]) ? "'" + s : s;
}
