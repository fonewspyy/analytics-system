using Analytics.Application.Common;
using Analytics.Application.Tasks;
using Analytics.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Analytics.Api.Controllers;

[ApiController]
[Route("api/tasks")]
public sealed class TasksController(TaskService tasks) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<TaskDto>> List(
        [FromQuery] long? projectId, [FromQuery] long? assigneeId,
        [FromQuery] TaskItemStatus? status, [FromQuery] TaskPriority? priority, [FromQuery] bool? overdue,
        [FromQuery] int? limit, [FromQuery] int? offset, CancellationToken ct) =>
        tasks.ListAsync(new TaskFilter(projectId, assigneeId, status, priority, overdue), new PageRequest(limit, offset), ct);

    [HttpGet("{id:long}")]
    public Task<TaskDto> Get(long id, CancellationToken ct) => tasks.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<TaskDto>> Create(CreateTaskRequest request, CancellationToken ct)
    {
        var task = await tasks.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = task.Id }, task);
    }

    [HttpPut("{id:long}")]
    public Task<TaskDto> Update(long id, UpdateTaskRequest request, CancellationToken ct) =>
        tasks.UpdateAsync(id, request, ct);

    [HttpPatch("{id:long}/status")]
    public Task<TaskDto> ChangeStatus(long id, ChangeTaskStatusRequest request, CancellationToken ct) =>
        tasks.ChangeStatusAsync(id, request.Status, ct);

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await tasks.DeleteAsync(id, ct);
        return NoContent();
    }
}
