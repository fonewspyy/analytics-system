using Analytics.Application.Common;
using Analytics.Application.Logs;
using Analytics.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Analytics.Api.Controllers;

/// <summary>Audit trail. Read-only: rows are written by the API itself.</summary>
[ApiController]
[Route("api/logs")]
public sealed class LogsController(ISystemLogRepository logs) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<SystemLogDto>> List(
        [FromQuery] LogSeverity? level, [FromQuery] string? action,
        [FromQuery] string? entityType, [FromQuery] long? entityId,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] int? limit, [FromQuery] int? offset, CancellationToken ct)
    {
        var page = await logs.ListAsync(
            new SystemLogFilter(level, action, entityType, entityId, from, to), new PageRequest(limit, offset), ct);
        return new(page.Items.Select(SystemLogDto.From).ToList(), page.Total, page.Limit, page.Offset);
    }
}
