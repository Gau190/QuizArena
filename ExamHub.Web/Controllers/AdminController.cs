using ExamHub.Core.Entities;
using ExamHub.Core.Enums;
using ExamHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ExamHub.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(ExamHubDbContext db, IMemoryCache cache) : AppController
{
    [HttpGet("/admin")]
    [HttpGet("/admin/dashboard")]
    [HttpGet("/admin/users")]
    public async Task<IActionResult> Users(string? q, UserRole? role)
    {
        var users = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            users = users.Where(x => x.FullName.Contains(q) || x.Username.Contains(q) || x.Email.Contains(q));
        }
        if (role.HasValue) users = users.Where(x => x.Role == role);
        return View(await users.OrderBy(x => x.Role).ThenBy(x => x.FullName).ToListAsync());
    }

    [HttpPost("/admin/users")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(string username, string email, string fullName, string password, UserRole role)
    {
        db.Users.Add(new User { Username = username, Email = email, FullName = fullName, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12), Role = role });
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Users));
    }

    [HttpPost("/admin/users/{id:guid}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUser(Guid id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is not null && user.Id != CurrentUserId)
        {
            user.IsActive = !user.IsActive;
            await db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Users));
    }

    [HttpGet("/admin/subjects")]
    public async Task<IActionResult> Subjects()
    {
        var subjects = await cache.GetOrCreateAsync("subjects_active_admin", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return await db.Subjects.AsNoTracking().Include(x => x.Creator).OrderBy(x => x.Name).ToListAsync();
        });
        return View(subjects);
    }

    [HttpPost("/admin/subjects")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSubject(string name, string? description)
    {
        db.Subjects.Add(new Subject { Name = name, Description = description, CreatedBy = CurrentUserId });
        await db.SaveChangesAsync();
        cache.Remove("subjects_active_admin");
        return RedirectToAction(nameof(Subjects));
    }

    [HttpPost("/admin/subjects/{id:int}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSubject(int id)
    {
        var subject = await db.Subjects.FindAsync(id);
        if (subject is not null)
        {
            subject.IsActive = !subject.IsActive;
            await db.SaveChangesAsync();
            cache.Remove("subjects_active_admin");
        }
        return RedirectToAction(nameof(Subjects));
    }
}
