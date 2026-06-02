using System.Security.Claims;
using ExamHub.Core.Interfaces;
using ExamHub.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExamHub.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await authService.LoginAsync(request);
        return result is null ? Unauthorized() : Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await authService.LogoutAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
        return NoContent();
    }
}
