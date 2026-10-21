using Analytics.Domain;
using Analytics.Infrastructure.Export;
using Analytics.Infrastructure.Persistence;

namespace Analytics.Tests;

public class DbEnumTests
{
    [Theory]
    [InlineData(TaskItemStatus.InProgress, "in_progress")]
    [InlineData(ProjectStatus.OnHold, "on_hold")]
    [InlineData(TaskPriority.Critical, "critical")]
    public void Maps_to_postgres_labels(Enum value, string label) => Assert.Equal(label, DbEnum.ToDb(value));

    [Fact]
    public void Every_member_round_trips()
    {
        foreach (var s in Enum.GetValues<TaskItemStatus>()) Assert.Equal(s, DbEnum.Parse<TaskItemStatus>(DbEnum.ToDb(s)));
        foreach (var s in Enum.GetValues<ProjectStatus>()) Assert.Equal(s, DbEnum.Parse<ProjectStatus>(DbEnum.ToDb(s)));
        foreach (var s in Enum.GetValues<TaskPriority>()) Assert.Equal(s, DbEnum.Parse<TaskPriority>(DbEnum.ToDb(s)));
        foreach (var s in Enum.GetValues<UserRole>()) Assert.Equal(s, DbEnum.Parse<UserRole>(DbEnum.ToDb(s)));
        foreach (var s in Enum.GetValues<LogSeverity>()) Assert.Equal(s, DbEnum.Parse<LogSeverity>(DbEnum.ToDb(s)));
    }

    [Fact]
    public void Unknown_label_fails_loudly() =>
        Assert.Throws<InvalidOperationException>(() => DbEnum.Parse<TaskItemStatus>("blocked"));
}

public class CsvWriterTests
{
    private sealed record Row(long Id, string Title, TaskItemStatus Status, DateOnly? Due, decimal Hours);

    private static async Task<string> Write(params Row[] rows)
    {
        using var writer = new StringWriter();
        await CsvWriter.WriteAsync(writer, rows, typeof(Row));
        return writer.ToString();
    }

    [Fact]
    public async Task Header_is_snake_case_and_values_use_api_formats()
    {
        var csv = await Write(new Row(1, "Plain", TaskItemStatus.InProgress, new DateOnly(2026, 10, 21), 1.5m));
        Assert.Equal("id,title,status,due,hours\r\n1,Plain,in_progress,2026-10-21,1.5\r\n", csv);
    }

    [Theory]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("งานภาษาไทย", "งานภาษาไทย")]
    public void Quotes_per_rfc4180(string input, string expected) =>
        Assert.Equal(expected, CsvWriter.Quote(CsvWriter.Format(input)));

    [Theory]
    [InlineData("=HYPERLINK(\"x\")")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1)")]
    public void Text_that_Excel_would_run_as_a_formula_is_neutralised(string input) =>
        Assert.StartsWith("'", CsvWriter.Format(input));

    [Fact]
    public void Negative_numbers_are_not_mangled() => Assert.Equal("-5.25", CsvWriter.Format(-5.25m));

    [Fact]
    public async Task Null_is_an_empty_cell()
    {
        var csv = await Write(new Row(2, "x", TaskItemStatus.Todo, null, 0m));
        Assert.Contains("2,x,todo,,0", csv);
    }
}
