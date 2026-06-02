using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace ExamHub.Web.Controllers;

public abstract class AppController : Controller
{
    protected Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
