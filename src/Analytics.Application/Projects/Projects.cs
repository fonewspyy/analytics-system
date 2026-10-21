using System.ComponentModel.DataAnnotations;
using Analytics.Application.Common;
using Analytics.Application.Users;
using Analytics.Domain;
using Analytics.Domain.Entities;

namespace Analytics.Application.Projects;

public sealed record ProjectDto(
    long Id, string Code, string Name, string? Description, long OwnerId, ProjectStatus Status,
    DateOnly StartDate, DateOnly? DueDate, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static ProjectDto From(Project p) =>
        new(p.Id, p.Code, p.Name, p.Description, p.OwnerId, p.Status, p.StartDate, p.DueDate, p.CreatedAt, p.UpdatedAt);
}

public sealed record CreateProjectRequest(
    [Required, RegularExpression("^[A-Za-z][A-Za-z0-9-]{1,19}$", ErrorMessage = "Code must be 2-20 letters, digits or '-', starting with a letter.")] string Code,
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [StringLength(4000)] string? Description,
    [Range(1, long.MaxValue)] long OwnerId,
    DateOnly? StartDate,
    DateOnly? DueDate);

public sealed record UpdateProjectRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [StringLength(4000)] string? Description,
    [Range(1, long.MaxValue)] long OwnerId,
    ProjectStatus Status,
    DateOnly StartDate,
    DateOnly? DueDate);

public sealed record ProjectFilter(ProjectStatus? Status, long? OwnerId);

public interface IProjectRepository
{
    Task<PagedResult<Project>> ListAsync(ProjectFilter filter, PageRequest page, CancellationToken ct);
    Task<Project?> GetAsync(long id, CancellationToken ct);
    Task<Project> CreateAsync(Project project, CancellationToken ct);
    Task<Project> UpdateAsync(Project project, CancellationToken ct);
    Task<bool> DeleteAsync(long id, CancellationToken ct);
    Task<(int Total, int Open)> CountTasksAsync(long id, CancellationToken ct);
}

public sealed class ProjectService(IProjectRepository projects, IUserRepository users, TimeProvider clock)
{
    public async Task<PagedResult<ProjectDto>> ListAsync(ProjectFilter filter, PageRequest page, CancellationToken ct)
    {
        var result = await projects.ListAsync(filter, page, ct);
        return new(result.Items.Select(ProjectDto.From).ToList(), result.Total, result.Limit, result.Offset);
    }

    public async Task<ProjectDto> GetAsync(long id, CancellationToken ct) =>
        ProjectDto.From(await projects.GetAsync(id, ct) ?? throw new NotFoundException("Project", id));

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct)
    {
        var start = request.StartDate ?? DateOnly.FromDateTime(clock.GetLocalNow().Date);
        EnsureDates(start, request.DueDate);
        await EnsureActiveOwnerAsync(request.OwnerId, ct);

        var project = new Project
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            Description = request.Description,
            OwnerId = request.OwnerId,
            StartDate = start,
            DueDate = request.DueDate,
        };
        return ProjectDto.From(await projects.CreateAsync(project, ct));
    }

    public async Task<ProjectDto> UpdateAsync(long id, UpdateProjectRequest request, CancellationToken ct)
    {
        var project = await projects.GetAsync(id, ct) ?? throw new NotFoundException("Project", id);
        EnsureDates(request.StartDate, request.DueDate);
        if (request.OwnerId != project.OwnerId)
            await EnsureActiveOwnerAsync(request.OwnerId, ct);

        if (request.Status != project.Status)
        {
            var (_, open) = await projects.CountTasksAsync(id, ct);
            project.ChangeStatus(request.Status, open);
        }

        project.Name = request.Name.Trim();
        project.Description = request.Description;
        project.OwnerId = request.OwnerId;
        project.StartDate = request.StartDate;
        project.DueDate = request.DueDate;
        return ProjectDto.From(await projects.UpdateAsync(project, ct));
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        var project = await projects.GetAsync(id, ct) ?? throw new NotFoundException("Project", id);
        var (total, _) = await projects.CountTasksAsync(id, ct);
        if (!project.CanBeDeleted(total))
            throw new ConflictException($"Project {project.Code} has {total} task(s) and is past planning; set its status to 'archived' instead of deleting it.");
        await projects.DeleteAsync(id, ct);
    }

    private static void EnsureDates(DateOnly start, DateOnly? due)
    {
        if (!Project.DatesAreValid(start, due))
            throw new RequestValidationException("dueDate", "Due date cannot be before the start date.");
    }

    private async Task EnsureActiveOwnerAsync(long ownerId, CancellationToken ct)
    {
        var owner = await users.GetAsync(ownerId, ct);
        if (owner is null || !owner.IsActive)
            throw new RequestValidationException("ownerId", $"User {ownerId} does not exist or is inactive.");
    }
}
