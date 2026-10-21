using Analytics.Application.Analytics;
using Analytics.Application.Common;
using Analytics.Application.Projects;
using Analytics.Application.Tasks;
using Analytics.Application.Users;
using Analytics.Domain;

namespace Analytics.Tests;

public class TaskServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);
    private readonly FakeUsers _users = new FakeUsers().With(1).With(2, active: false);
    private readonly FakeProjects _projects = new FakeProjects()
        .With(10, ProjectStatus.Active)
        .With(11, ProjectStatus.Completed);
    private readonly FakeTasks _tasks = new();

    private TaskService Service() => new(_tasks, _projects, _users, new FixedClock(Now));

    private static CreateTaskRequest Request(long projectId, long? assignee = null, TaskItemStatus status = TaskItemStatus.Todo) =>
        new(projectId, assignee, "  Write report  ", null, status);

    [Fact]
    public async Task Creates_task_with_trimmed_title()
    {
        var dto = await Service().CreateAsync(Request(10, assignee: 1), default);
        Assert.Equal("Write report", dto.Title);
        Assert.Equal(TaskItemStatus.Todo, dto.Status);
    }

    [Fact]
    public async Task Created_as_done_gets_completion_time()
    {
        var dto = await Service().CreateAsync(Request(10, status: TaskItemStatus.Done), default);
        Assert.Equal(Now, dto.CompletedAt);
    }

    [Fact]
    public async Task Unknown_project_is_a_field_error()
    {
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() => Service().CreateAsync(Request(99), default));
        Assert.True(ex.Errors.ContainsKey("projectId"));
    }

    [Fact]
    public async Task Completed_project_rejects_new_tasks()
    {
        await Assert.ThrowsAsync<DomainRuleException>(() => Service().CreateAsync(Request(11), default));
        Assert.Null(_tasks.Saved);
    }

    [Theory]
    [InlineData(2L)]   // inactive
    [InlineData(99L)]  // missing
    public async Task Assignee_must_be_an_active_user(long assignee)
    {
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() => Service().CreateAsync(Request(10, assignee), default));
        Assert.True(ex.Errors.ContainsKey("assigneeId"));
    }
}

public class ProjectServiceTests
{
    private readonly FakeUsers _users = new FakeUsers().With(1);

    [Fact]
    public async Task Deleting_a_worked_on_project_is_refused()
    {
        var projects = new FakeProjects().With(10, ProjectStatus.Active, totalTasks: 3);
        var service = new ProjectService(projects, _users, TimeProvider.System);

        await Assert.ThrowsAsync<ConflictException>(() => service.DeleteAsync(10, default));
        Assert.False(projects.Deleted);
    }

    [Fact]
    public async Task Planning_project_can_be_deleted()
    {
        var projects = new FakeProjects().With(10, ProjectStatus.Planning, totalTasks: 3);
        await new ProjectService(projects, _users, TimeProvider.System).DeleteAsync(10, default);
        Assert.True(projects.Deleted);
    }

    [Fact]
    public async Task Code_is_upper_cased_and_due_date_checked()
    {
        var service = new ProjectService(new FakeProjects(), _users, TimeProvider.System);
        var dto = await service.CreateAsync(new CreateProjectRequest("ab-1", "x", null, 1, new DateOnly(2026, 1, 1), null), default);
        Assert.Equal("AB-1", dto.Code);

        var ex = await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(
            new CreateProjectRequest("ab-2", "x", null, 1, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1)), default));
        Assert.True(ex.Errors.ContainsKey("dueDate"));
    }
}

public class UserServiceTests
{
    [Fact]
    public async Task User_who_owns_projects_cannot_be_deleted()
    {
        var users = new FakeUsers().With(1);
        users.OwnedProjects[1] = 2;
        await Assert.ThrowsAsync<ConflictException>(() => new UserService(users).DeleteAsync(1, default));
        Assert.True(users.Rows.ContainsKey(1));
    }

    [Fact]
    public async Task Deleting_missing_user_is_not_found() =>
        await Assert.ThrowsAsync<NotFoundException>(() => new UserService(new FakeUsers()).DeleteAsync(42, default));
}

public class AnalyticsServiceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public async Task Trend_days_are_bounded(int days)
    {
        var service = new AnalyticsService(null!); // validation runs before the query
        await Assert.ThrowsAsync<RequestValidationException>(() => service.TrendAsync(days, default));
    }
}

public class PageRequestTests
{
    [Theory]
    [InlineData(null, null, 50, 0)]
    [InlineData(0, -5, 1, 0)]
    [InlineData(5000, 10, PageRequest.MaxLimit, 10)]
    public void Limits_are_clamped(int? limit, int? offset, int expectedLimit, int expectedOffset)
    {
        var page = new PageRequest(limit, offset);
        Assert.Equal(expectedLimit, page.Limit);
        Assert.Equal(expectedOffset, page.Offset);
    }
}
