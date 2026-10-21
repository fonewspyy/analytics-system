using Analytics.Application.Common;
using Analytics.Domain;

namespace Analytics.Application.Analytics;

public sealed record ProjectSummaryRow(
    long ProjectId, string Code, string Name, ProjectStatus Status, string OwnerName, DateOnly? DueDate,
    int TotalTasks, int Todo, int InProgress, int Done, int Cancelled, int Overdue,
    decimal? ProgressPercent, decimal? AvgCompletionHours, decimal RemainingEstimateHours);

public sealed record WorkloadRow(
    long UserId, string FullName, UserRole Role,
    int OpenTasks, int OpenHighPriority, int Overdue, int DoneLast30Days,
    decimal OpenEstimateHours, int LoadRank);

public sealed record TrendRow(DateOnly Day, int Created, int Completed, int NetOpenChange);

public interface IAnalyticsQueries
{
    Task<IReadOnlyList<ProjectSummaryRow>> ProjectSummaryAsync(ProjectStatus? status, CancellationToken ct);
    Task<IReadOnlyList<WorkloadRow>> WorkloadAsync(CancellationToken ct);
    Task<IReadOnlyList<TrendRow>> TrendAsync(int days, CancellationToken ct);
}

public sealed class AnalyticsService(IAnalyticsQueries queries)
{
    public const int MaxTrendDays = 365;

    public Task<IReadOnlyList<ProjectSummaryRow>> ProjectSummaryAsync(ProjectStatus? status, CancellationToken ct) =>
        queries.ProjectSummaryAsync(status, ct);

    public Task<IReadOnlyList<WorkloadRow>> WorkloadAsync(CancellationToken ct) =>
        queries.WorkloadAsync(ct);

    public Task<IReadOnlyList<TrendRow>> TrendAsync(int days, CancellationToken ct)
    {
        if (days is < 1 or > MaxTrendDays)
            throw new RequestValidationException("days", $"days must be between 1 and {MaxTrendDays}.");
        return queries.TrendAsync(days, ct);
    }
}
