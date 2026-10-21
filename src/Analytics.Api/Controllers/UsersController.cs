using Analytics.Application.Common;
using Analytics.Application.Users;
using Analytics.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Analytics.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(UserService users) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<UserDto>> List(
        [FromQuery] UserRole? role, [FromQuery] bool? isActive,
        [FromQuery] int? limit, [FromQuery] int? offset, CancellationToken ct) =>
        users.ListAsync(new UserFilter(role, isActive), new PageRequest(limit, offset), ct);

    [HttpGet("{id:long}")]
    public Task<UserDto> Get(long id, CancellationToken ct) => users.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var user = await users.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }

    [HttpPut("{id:long}")]
    public Task<UserDto> Update(long id, UpdateUserRequest request, CancellationToken ct) =>
        users.UpdateAsync(id, request, ct);

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await users.DeleteAsync(id, ct);
        return NoContent();
    }
}
