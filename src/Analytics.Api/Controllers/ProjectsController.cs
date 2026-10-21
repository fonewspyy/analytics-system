using Analytics.Application.Common;
using Analytics.Application.Projects;
using Analytics.Application.Tasks;
using Analytics.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Analytics.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController(ProjectService projects, TaskService tasks) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ProjectDto>> List(
        [FromQuery] ProjectStatus? status, [FromQuery] long? ownerId,
        [FromQuery] int? limit, [FromQuery] int? offset, CancellationToken ct) =>
        projects.ListAsync(new ProjectFilter(status, ownerId), new PageRequest(limit, offset), ct);

    [HttpGet("{id:long}")]
    public Task<ProjectDto> Get(long id, CancellationToken ct) => projects.GetAsync(id, ct);

    [HttpGet("{id:long}/tasks")]
    public async Task<PagedResult<TaskDto>> Tasks(
        long id, [FromQuery] TaskItemStatus? status,
        [FromQuery] int? limit, [FromQuery] int? offset, CancellationToken ct)
    {
        await projects.GetAsync(id, ct); // 404 for an unknown project rather than an empty list
        return await tasks.ListAsync(new TaskFilter(id, null, status, null, null), new PageRequest(limit, offset), ct);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var project = await projects.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, project);
    }

    [HttpPut("{id:long}")]
    public Task<ProjectDto> Update(long id, UpdateProjectRequest request, CancellationToken ct) =>
        projects.UpdateAsync(id, request, ct);

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await projects.DeleteAsync(id, ct);
        return NoContent();
    }
}
