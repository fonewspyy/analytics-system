using System.ComponentModel.DataAnnotations;
using Analytics.Application.Common;
using Analytics.Domain;
using Analytics.Domain.Entities;

namespace Analytics.Application.Users;

public sealed record UserDto(
    long Id, string Email, string FullName, UserRole Role, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static UserDto From(User u) =>
        new(u.Id, u.Email, u.FullName, u.Role, u.IsActive, u.CreatedAt, u.UpdatedAt);
}

public sealed record CreateUserRequest(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(200, MinimumLength = 1)] string FullName,
    UserRole Role = UserRole.Member);

public sealed record UpdateUserRequest(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(200, MinimumLength = 1)] string FullName,
    UserRole Role,
    bool IsActive);

public sealed record UserFilter(UserRole? Role, bool? IsActive);

public interface IUserRepository
{
    Task<PagedResult<User>> ListAsync(UserFilter filter, PageRequest page, CancellationToken ct);
    Task<User?> GetAsync(long id, CancellationToken ct);
    Task<User> CreateAsync(User user, CancellationToken ct);
    Task<User> UpdateAsync(User user, CancellationToken ct);
    Task<bool> DeleteAsync(long id, CancellationToken ct);
    Task<int> CountOwnedProjectsAsync(long id, CancellationToken ct);
}

public sealed class UserService(IUserRepository users)
{
    public async Task<PagedResult<UserDto>> ListAsync(UserFilter filter, PageRequest page, CancellationToken ct)
    {
        var result = await users.ListAsync(filter, page, ct);
        return new(result.Items.Select(UserDto.From).ToList(), result.Total, result.Limit, result.Offset);
    }

    public async Task<UserDto> GetAsync(long id, CancellationToken ct) =>
        UserDto.From(await users.GetAsync(id, ct) ?? throw new NotFoundException("User", id));

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var user = new User
        {
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            Role = request.Role,
        };
        return UserDto.From(await users.CreateAsync(user, ct));
    }

    public async Task<UserDto> UpdateAsync(long id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await users.GetAsync(id, ct) ?? throw new NotFoundException("User", id);
        user.Email = request.Email.Trim();
        user.FullName = request.FullName.Trim();
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        return UserDto.From(await users.UpdateAsync(user, ct));
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        var owned = await users.CountOwnedProjectsAsync(id, ct);
        if (owned > 0)
            throw new ConflictException($"User {id} owns {owned} project(s); reassign them or deactivate the user instead.");
        if (!await users.DeleteAsync(id, ct))
            throw new NotFoundException("User", id);
    }
}
