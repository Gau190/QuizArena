using System.ComponentModel.DataAnnotations;
using QuizArena.Core.Entities;
using QuizArena.Core.Enums;
using QuizArena.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace QuizArena.Web.Controllers.Api;

public record UserDto(Guid Id, string Username, string Email, string FullName, UserRole Role, bool IsActive, DateTime CreatedAt, DateTime? LastLoginAt);

public record CreateUserRequest(
    [Required, StringLength(50, MinimumLength = 3)] string Username,
    [Required, EmailAddress, StringLength(120)] string Email,
    [Required, StringLength(120)] string FullName,
    [Required, StringLength(100, MinimumLength = 6)] string Password,
    UserRole Role);

public record UpdateUserRequest(
    [Required, EmailAddress, StringLength(120)] string Email,
    [Required, StringLength(120)] string FullName,
    UserRole Role,
    bool IsActive);

public record SubjectDto(int Id, string Name, string? Description, string? Code, bool IsActive);

public record CreateSubjectRequest([Required, StringLength(120)] string Name, [StringLength(500)] string? Description, [StringLength(20)] string? Code);

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public class AdminApiController(QuizArenaDbContext db) : ControllerBase
{
    private static UserDto ToDto(User x) => new(x.Id, x.Username, x.Email, x.FullName, x.Role, x.IsActive, x.CreatedAt, x.LastLoginAt);

    [HttpGet("users")]
    public async Task<IActionResult> Users() =>
        Ok(await db.Users.AsNoTracking().OrderBy(x => x.Role).ThenBy(x => x.FullName)
            .Select(x => new UserDto(x.Id, x.Username, x.Email, x.FullName, x.Role, x.IsActive, x.CreatedAt, x.LastLoginAt))
            .ToListAsync());

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest input)
    {
        var username = input.Username.Trim();
        if (await db.Users.AnyAsync(x => x.Username == username))
        {
            return Conflict(new { message = "Tên đăng nhập đã tồn tại." });
        }

        var user = new User
        {
            Username = username,
            Email = input.Email.Trim(),
            FullName = input.FullName.Trim(),
            Role = input.Role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(input.Password, 12)
        };
        db.Users.Add(user);
        if (user.Role == UserRole.Student)
        {
            db.StudentProfiles.Add(new StudentProfile { UserId = user.Id, StudentCode = $"TS-{user.Id.ToString()[..4].ToUpperInvariant()}", Conduct = "Tốt" });
        }

        await db.SaveChangesAsync();
        return Created($"/api/v1/admin/users/{user.Id}", ToDto(user));
    }

    [HttpPut("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, UpdateUserRequest input)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        user.FullName = input.FullName.Trim();
        user.Email = input.Email.Trim();
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
    public async Task<IActionResult> Subjects() =>
        Ok(await db.Subjects.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new SubjectDto(x.Id, x.Name, x.Description, x.Code, x.IsActive))
            .ToListAsync());

    [HttpPost("subjects")]
    public async Task<IActionResult> CreateSubject(CreateSubjectRequest input)
    {
        var subject = new Subject
        {
            Name = input.Name.Trim(),
            Description = input.Description?.Trim(),
            Code = input.Code?.Trim(),
            CreatedBy = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value)
        };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();
        return Created($"/api/v1/admin/subjects/{subject.Id}", new SubjectDto(subject.Id, subject.Name, subject.Description, subject.Code, subject.IsActive));
    }
}
