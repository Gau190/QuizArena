using ExamHub.Core.Models;

namespace ExamHub.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(Guid userId, CancellationToken cancellationToken = default);
}
