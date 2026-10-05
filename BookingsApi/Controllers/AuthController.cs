using BookingsApi.Dtos;
using BookingsApi.Infrastructure;
using BookingsApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingsApi.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var response = await auth.RegisterAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        await auth.LoginAsync(request, ct);

    /// <summary>Returns the user described by the token (no DB hit).</summary>
    [HttpGet("me")]
    public ActionResult<UserDto> Me() =>
        new UserDto(
            User.GetUserId(),
            User.FindFirst("email")?.Value ?? "",
            User.FindFirst("name")?.Value ?? "",
            User.FindFirst("role")?.Value ?? "");
}
