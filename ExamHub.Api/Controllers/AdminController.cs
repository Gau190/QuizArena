using ExamHub.Core.Entities;
using ExamHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamHub.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(ExamHubDbContext db) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> Users() => Ok(await db.Users.AsNoTracking().OrderBy(x => x.Role).ThenBy(x => x.FullName).ToListAsync());

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(User input)
    {
        input.PasswordHash = BCrypt.Net.BCrypt.HashPassword(input.PasswordHash, 12);
        db.Users.Add(input);
        await db.SaveChangesAsync();
        return Created($"/api/admin/users/{input.Id}", input);
    }

    [HttpPut("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, User input)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        user.FullName = input.FullName;
        user.Email = input.Email;
        user.Role = input.Role;
        user.IsActive = input.IsActive;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        user.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("subjects")]
    public async Task<IActionResult> Subjects() => Ok(await db.Subjects.AsNoTracking().OrderBy(x => x.Name).ToListAsync());

    [HttpPost("subjects")]
    public async Task<IActionResult> CreateSubject(Subject input)
    {
        db.Subjects.Add(input);
        await db.SaveChangesAsync();
        return Created($"/api/admin/subjects/{input.Id}", input);
    }
}
