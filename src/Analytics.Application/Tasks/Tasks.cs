using System.ComponentModel.DataAnnotations;
using Analytics.Application.Common;
using Analytics.Application.Projects;
using Analytics.Application.Users;
using Analytics.Domain;
using Analytics.Domain.Entities;

namespace Analytics.Application.Tasks;

public sealed record TaskDto(
    long Id, long ProjectId, long? AssigneeId, string Title, string? Description,
    TaskItemStatus Status, TaskPriority Priority, decimal? EstimateHours, DateOnly? DueDate,
    DateTimeOffset? CompletedAt, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static TaskDto From(TaskItem t) =>
        new(t.Id, t.ProjectId, t.AssigneeId, t.Title, t.Description, t.Status, t.Priority,
            t.EstimateHours, t.DueDate, t.CompletedAt, t.CreatedAt, t.UpdatedAt);
}

public sealed record CreateTaskRequest(
    [Range(1, long.MaxValue)] long ProjectId,
    long? AssigneeId,
    [Required, StringLength(300, MinimumLength = 1)] string Title,
    [StringLength(4000)] string? Description,
    TaskItemStatus Status = TaskItemStatus.Todo,
    TaskPriority Priority = TaskPriority.Medium,
    [Range(0, 9999)] decimal? EstimateHours = null,
    DateOnly? DueDate = null);

public sealed record UpdateTaskRequest(
    long? AssigneeId,
    [Required, StringLength(300, MinimumLength = 1)] string Title,
    [StringLength(4000)] string? Description,
    TaskItemStatus Status,
    TaskPriority Priority,
    [Range(0, 9999)] decimal? EstimateHours,
    DateOnly? DueDate);

public sealed record ChangeTaskStatusRequest(TaskItemStatus Status);

public sealed record TaskFilter(long? ProjectId, long? AssigneeId, TaskItemStatus? Status, TaskPriority? Priority, bool? Overdue);

public interface ITaskRepository
{
    Task<PagedResult<TaskItem>> ListAsync(TaskFilter filter, PageRequest page, CancellationToken ct);
    Task<TaskItem?> GetAsync(long id, CancellationToken ct);
    Task<TaskItem> CreateAsync(TaskItem task, CancellationToken ct);
    Task<TaskItem> UpdateAsync(TaskItem task, CancellationToken ct);
    Task<bool> DeleteAsync(long id, CancellationToken ct);
}

public sealed class TaskService(ITaskRepository tasks, IProjectRepository projects, IUserRepository users, TimeProvider clock)
{
    public async Task<PagedResult<TaskDto>> ListAsync(TaskFilter filter, PageRequest page, CancellationToken ct)
    {
        var result = await tasks.ListAsync(filter, page, ct);
        return new(result.Items.Select(TaskDto.From).ToList(), result.Total, result.Limit, result.Offset);
    }

    public async Task<TaskDto> GetAsync(long id, CancellationToken ct) =>
        TaskDto.From(await tasks.GetAsync(id, ct) ?? throw new NotFoundException("Task", id));

    public async Task<TaskDto> CreateAsync(CreateTaskRequest request, CancellationToken ct)
    {
        var project = await projects.GetAsync(request.ProjectId, ct)
            ?? throw new RequestValidationException("projectId", $"Project {request.ProjectId} does not exist.");
        if (!project.AcceptsNewTasks)
            throw new DomainRuleException($"Project {project.Code} is {project.Status}; it does not accept new tasks.");
        await EnsureAssignableAsync(request.AssigneeId, ct);

        var task = new TaskItem
        {
            ProjectId = request.ProjectId,
            AssigneeId = request.AssigneeId,
            Title = request.Title.Trim(),
            Description = request.Description,
            Priority = request.Priority,
            EstimateHours = request.EstimateHours,
            DueDate = request.DueDate,
        };
        task.ChangeStatus(request.Status, clock.GetUtcNow());
        return TaskDto.From(await tasks.CreateAsync(task, ct));
    }

    public async Task<TaskDto> UpdateAsync(long id, UpdateTaskRequest request, CancellationToken ct)
    {
        var task = await tasks.GetAsync(id, ct) ?? throw new NotFoundException("Task", id);
        if (request.AssigneeId != task.AssigneeId)
            await EnsureAssignableAsync(request.AssigneeId, ct);

        task.ChangeStatus(request.Status, clock.GetUtcNow());
        task.AssigneeId = request.AssigneeId;
        task.Title = request.Title.Trim();
        task.Description = request.Description;
        task.Priority = request.Priority;
        task.EstimateHours = request.EstimateHours;
        task.DueDate = request.DueDate;
        return TaskDto.From(await tasks.UpdateAsync(task, ct));
    }

    public async Task<TaskDto> ChangeStatusAsync(long id, TaskItemStatus status, CancellationToken ct)
    {
        var task = await tasks.GetAsync(id, ct) ?? throw new NotFoundException("Task", id);
        task.ChangeStatus(status, clock.GetUtcNow());
        return TaskDto.From(await tasks.UpdateAsync(task, ct));
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        if (!await tasks.DeleteAsync(id, ct))
            throw new NotFoundException("Task", id);
    }

    private async Task EnsureAssignableAsync(long? assigneeId, CancellationToken ct)
    {
        if (assigneeId is null) return;
        var user = await users.GetAsync(assigneeId.Value, ct);
        if (user is null || !user.IsActive)
            throw new RequestValidationException("assigneeId", $"User {assigneeId} does not exist or is inactive.");
    }
}
