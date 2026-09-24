using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace QuizArena.Web.Controllers;

public abstract class AppController : Controller
{
    protected Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
