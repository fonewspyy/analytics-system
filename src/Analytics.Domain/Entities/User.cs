namespace Analytics.Domain.Entities;

public sealed class User
{
    public long Id { get; init; }
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public UserRole Role { get; set; } = UserRole.Member;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
