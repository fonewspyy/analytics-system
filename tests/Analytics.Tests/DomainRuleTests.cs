using Analytics.Domain;
using Analytics.Domain.Entities;

namespace Analytics.Tests;

public class TaskItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);

    private static TaskItem NewTask() => new() { Id = 1, ProjectId = 1, Title = "t" };

    [Fact]
    public void Done_sets_completed_at()
    {
        var task = NewTask();
        task.ChangeStatus(TaskItemStatus.Done, Now);
        Assert.Equal(Now, task.CompletedAt);
    }

    [Fact]
    public void Leaving_done_clears_completed_at()
    {
        var task = NewTask();
        task.ChangeStatus(TaskItemStatus.Done, Now);
        task.ChangeStatus(TaskItemStatus.InProgress, Now.AddHours(1));
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void Same_status_keeps_original_completion_time()
    {
        var task = NewTask();
        task.ChangeStatus(TaskItemStatus.Done, Now);
        task.ChangeStatus(TaskItemStatus.Done, Now.AddDays(1));
        Assert.Equal(Now, task.CompletedAt);
    }

    [Theory]
    [InlineData(TaskItemStatus.Todo)]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Done)]
    public void Cancelled_is_terminal(TaskItemStatus next)
    {
        var task = NewTask();
        task.ChangeStatus(TaskItemStatus.Cancelled, Now);
        Assert.Throws<DomainRuleException>(() => task.ChangeStatus(next, Now));
    }
}

public class ProjectTests
{
    private static Project NewProject(ProjectStatus status) =>
        Project.Load(new Project { Id = 1, Code = "P1", Name = "p" }, status);

    [Fact]
    public void Cannot_complete_with_open_tasks()
    {
        var project = NewProject(ProjectStatus.Active);
        Assert.Throws<DomainRuleException>(() => project.ChangeStatus(ProjectStatus.Completed, openTaskCount: 2));
    }

    [Fact]
    public void Can_complete_when_nothing_is_open()
    {
        var project = NewProject(ProjectStatus.Active);
        project.ChangeStatus(ProjectStatus.Completed, openTaskCount: 0);
        Assert.Equal(ProjectStatus.Completed, project.Status);
    }

    [Theory]
    [InlineData("2026-01-01", null, true)]
    [InlineData("2026-01-01", "2026-01-01", true)]
    [InlineData("2026-01-02", "2026-01-01", false)]
    public void Due_date_not_before_start(string start, string? due, bool valid) =>
        Assert.Equal(valid, Project.DatesAreValid(DateOnly.Parse(start), due is null ? null : DateOnly.Parse(due)));

    [Theory]
    [InlineData(ProjectStatus.Planning, 5, true)]
    [InlineData(ProjectStatus.Active, 0, true)]
    [InlineData(ProjectStatus.Active, 1, false)]
    public void Only_untouched_projects_can_be_deleted(ProjectStatus status, int tasks, bool allowed) =>
        Assert.Equal(allowed, NewProject(status).CanBeDeleted(tasks));
}
