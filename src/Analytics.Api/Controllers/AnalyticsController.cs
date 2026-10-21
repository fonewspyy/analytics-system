using Analytics.Application.Analytics;
using Analytics.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Analytics.Api.Controllers;

/// <summary>Read-only reports. Every endpoint also serves CSV (?format=csv).</summary>
[ApiController]
[Route("api/analytics")]
public sealed class AnalyticsController(AnalyticsService analytics) : ControllerBase
{
    /// <summary>Per project: task counts by status, progress %, overdue, average completion time, remaining estimate.</summary>
    [HttpGet("projects")]
    public Task<IReadOnlyList<ProjectSummaryRow>> Projects([FromQuery] ProjectStatus? status, CancellationToken ct) =>
        analytics.ProjectSummaryAsync(status, ct);

    /// <summary>Per active user: open work, high-priority and overdue counts, throughput, ranked by load.</summary>
    [HttpGet("workload")]
    public Task<IReadOnlyList<WorkloadRow>> Workload(CancellationToken ct) => analytics.WorkloadAsync(ct);

    /// <summary>Tasks created vs completed per day, with the running change in open work.</summary>
    [HttpGet("trend")]
    public Task<IReadOnlyList<TrendRow>> Trend([FromQuery] int days = 30, CancellationToken ct = default) =>
        analytics.TrendAsync(days, ct);
}
