using System.ComponentModel.DataAnnotations;
using QuizArena.Core.Enums;

namespace QuizArena.Core.Models;

public record LoginRequest(
    [Required, StringLength(50)] string Username,
    [Required, StringLength(100)] string Password,
    bool RememberMe = false);

public record AuthResult(
    Guid UserId,
    string Username,
    string FullName,
    UserRole Role,
    string Token,
    string TokenHash,
    DateTime ExpiresAt);
