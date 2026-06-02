using System.ComponentModel.DataAnnotations;
using ExamHub.Core.Enums;

namespace ExamHub.Core.Models;

public record LoginRequest(
    [Required, StringLength(50)] string Username,
    [Required, StringLength(100)] string Password);

public record AuthResult(
    Guid UserId,
    string Username,
    string FullName,
    UserRole Role,
    string Token,
    string TokenHash,
    DateTime ExpiresAt);
