using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ExamHub.Web.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ExamHub.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction("Login", "Account");
        var role = User.FindFirstValue(ClaimTypes.Role);
        return role switch
        {
            "Admin" => LocalRedirect("/admin"),
            "Teacher" => LocalRedirect("/teacher"),
            "Student" => LocalRedirect("/student"),
            _ => RedirectToAction("Login", "Account")
        };
    }

    [Authorize]
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
