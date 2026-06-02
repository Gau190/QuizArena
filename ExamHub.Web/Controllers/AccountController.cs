using System.Security.Claims;
using ExamHub.Core.Interfaces;
using ExamHub.Core.Models;
using ExamHub.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExamHub.Web.Controllers;

public class AccountController(IAuthService authService) : Controller
{
    [HttpGet]
    public IActionResult Login() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Home") : View(new LoginViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Error = "Vui lòng nhập đầy đủ thông tin.";
            return View(model);
        }

        var request = new LoginRequest(model.Username, model.Password, model.RememberMe);
        var result = await authService.LoginAsync(request);
        if (result is null)
        {
            ViewBag.Error = "Sai tên đăng nhập hoặc mật khẩu.";
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new(ClaimTypes.Name, result.Username),
            new("full_name", result.FullName),
            new(ClaimTypes.Role, result.Role.ToString()),
            new("remember_me", model.RememberMe ? "true" : "false"),
            new("token_hash", result.TokenHash)
        };
        var expiresAt = new DateTimeOffset(result.ExpiresAt);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = expiresAt
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            authProperties);

        Response.Cookies.Append("ExamHubToken", result.Token, new CookieOptions { HttpOnly = false, Secure = Request.IsHttps, SameSite = SameSiteMode.Strict, Expires = expiresAt });
        return LocalRedirect(returnUrl ?? RoleDestination(result.Role.ToString()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(id, out var userId))
        {
            await authService.LogoutAsync(userId);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete("ExamHubToken");
        return RedirectToAction(nameof(Login));
    }

    private static string RoleDestination(string role) => role switch
    {
        "Admin" => "/admin",
        "Teacher" => "/teacher",
        "Student" => "/student",
        _ => "/"
    };
}
