using Analytics.Application.Common;
using Analytics.Application.Projects;
using Analytics.Application.Tasks;
using Analytics.Application.Users;
using Analytics.Domain;
using Analytics.Domain.Entities;

namespace Analytics.Tests;

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FakeUsers : IUserRepository
{
    public Dictionary<long, User> Rows { get; } = [];
    public Dictionary<long, int> OwnedProjects { get; } = [];

    public FakeUsers With(long id, bool active = true)
    {
        Rows[id] = new User { Id = id, Email = $"u{id}@example.com", FullName = $"User {id}", IsActive = active };
        return this;
    }

    public Task<User?> GetAsync(long id, CancellationToken ct) => Task.FromResult(Rows.GetValueOrDefault(id));
    public Task<int> CountOwnedProjectsAsync(long id, CancellationToken ct) => Task.FromResult(OwnedProjects.GetValueOrDefault(id));
    public Task<bool> DeleteAsync(long id, CancellationToken ct) => Task.FromResult(Rows.Remove(id));
    public Task<User> CreateAsync(User user, CancellationToken ct) => Task.FromResult(user);
    public Task<User> UpdateAsync(User user, CancellationToken ct) => Task.FromResult(user);
    public Task<PagedResult<User>> ListAsync(UserFilter filter, PageRequest page, CancellationToken ct) => throw new NotSupportedException();
}

internal sealed class FakeProjects : IProjectRepository
{
    public Dictionary<long, Project> Rows { get; } = [];
    public Dictionary<long, (int Total, int Open)> Counts { get; } = [];
    public bool Deleted { get; private set; }

    public FakeProjects With(long id, ProjectStatus status, int totalTasks = 0, int openTasks = 0)
    {
        Rows[id] = Project.Load(new Project { Id = id, Code = $"P{id}", Name = $"Project {id}", OwnerId = 1 }, status);
        Counts[id] = (totalTasks, openTasks);
        return this;
    }

    public Task<Project?> GetAsync(long id, CancellationToken ct) => Task.FromResult(Rows.GetValueOrDefault(id));
    public Task<(int Total, int Open)> CountTasksAsync(long id, CancellationToken ct) => Task.FromResult(Counts.GetValueOrDefault(id));
    public Task<bool> DeleteAsync(long id, CancellationToken ct) { Deleted = true; return Task.FromResult(true); }
    public Task<Project> CreateAsync(Project project, CancellationToken ct) => Task.FromResult(project);
    public Task<Project> UpdateAsync(Project project, CancellationToken ct) => Task.FromResult(project);
    public Task<PagedResult<Project>> ListAsync(ProjectFilter filter, PageRequest page, CancellationToken ct) => throw new NotSupportedException();
}

internal sealed class FakeTasks : ITaskRepository
{
    public TaskItem? Saved { get; private set; }

    public Task<TaskItem> CreateAsync(TaskItem task, CancellationToken ct) { Saved = task; return Task.FromResult(task); }
    public Task<TaskItem> UpdateAsync(TaskItem task, CancellationToken ct) { Saved = task; return Task.FromResult(task); }
    public Task<TaskItem?> GetAsync(long id, CancellationToken ct) => Task.FromResult(Saved);
    public Task<bool> DeleteAsync(long id, CancellationToken ct) => Task.FromResult(false);
    public Task<PagedResult<TaskItem>> ListAsync(TaskFilter filter, PageRequest page, CancellationToken ct) => throw new NotSupportedException();
}
