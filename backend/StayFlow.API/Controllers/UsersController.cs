using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Application.Auth;
using StayFlow.Application.Users;

namespace StayFlow.API.Controllers;

/// <summary>Personel yonetimi - sadece otel sahibi.</summary>
[ApiController]
[Authorize(Roles = Roles.Owner)]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;

    public UsersController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll(CancellationToken ct) =>
        Ok(await _userService.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct) =>
        Ok(await _userService.CreateAsync(request, ct));
}
