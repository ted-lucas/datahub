using DataHub.Core.Constants;
using DataHub.Core.DTOs.Users;
using DataHub.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataHub.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users) => _users = users;

    [HttpGet]
    [Authorize(Policy = Permissions.UsersRead)]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await _users.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.UsersRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(id, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.UsersManage)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var dto = await _users.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.UsersManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var dto = await _users.UpdateAsync(id, request, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.UsersManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await _users.DeleteAsync(id, ct)) ? NoContent() : NotFound();
}
