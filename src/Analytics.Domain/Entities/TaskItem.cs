namespace Analytics.Domain.Entities;

/// <summary>A task. Named TaskItem to avoid clashing with System.Threading.Tasks.Task.</summary>
public sealed class TaskItem
{
    public long Id { get; init; }
    public long ProjectId { get; init; }
    public long? AssigneeId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public TaskItemStatus Status { get; private set; } = TaskItemStatus.Todo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public decimal? EstimateHours { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    public bool IsOpen => Status is TaskItemStatus.Todo or TaskItemStatus.InProgress;

    /// <summary>
    /// Moves the task to <paramref name="next"/>. Keeps <see cref="CompletedAt"/> in step with
    /// the status, which the schema enforces with <c>ck_tasks_completed_at</c>.
    /// </summary>
    public void ChangeStatus(TaskItemStatus next, DateTimeOffset now)
    {
        if (next == Status) return;
        if (Status == TaskItemStatus.Cancelled)
            throw new DomainRuleException($"Task {Id} is cancelled; create a new task instead of reopening it.");

        Status = next;
        CompletedAt = next == TaskItemStatus.Done ? now : null;
    }

    /// <summary>Rehydrate from storage without running transition rules.</summary>
    public static TaskItem Load(TaskItem t, TaskItemStatus status, DateTimeOffset? completedAt)
    {
        t.Status = status;
        t.CompletedAt = completedAt;
        return t;
    }
}
