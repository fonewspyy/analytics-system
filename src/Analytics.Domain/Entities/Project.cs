namespace Analytics.Domain.Entities;

public sealed class Project
{
    public long Id { get; init; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public long OwnerId { get; set; }
    public ProjectStatus Status { get; private set; } = ProjectStatus.Planning;
    public DateOnly StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Same rule as <c>ck_projects_dates</c> in the schema; checked here to return a field-level 400.</summary>
    public static bool DatesAreValid(DateOnly start, DateOnly? due) => due is null || due >= start;

    public bool AcceptsNewTasks => Status is not (ProjectStatus.Completed or ProjectStatus.Archived);

    /// <summary>Only a project nobody has worked on can be hard-deleted; anything else is archived.</summary>
    public bool CanBeDeleted(int taskCount) => Status == ProjectStatus.Planning || taskCount == 0;

    public void ChangeStatus(ProjectStatus next, int openTaskCount)
    {
        if (next == ProjectStatus.Completed && openTaskCount > 0)
            throw new DomainRuleException($"Project {Code} still has {openTaskCount} open task(s); finish or cancel them before completing it.");
        Status = next;
    }

    /// <summary>Rehydrate from storage without running transition rules.</summary>
    public static Project Load(Project p, ProjectStatus status)
    {
        p.Status = status;
        return p;
    }
}
