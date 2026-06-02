using ExamHub.Core.Entities;
using ExamHub.Core.Interfaces;
using ExamHub.Core.Models;
using ExamHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ExamHub.Infrastructure.Services;

public class AuthService(ExamHubDbContext db, JwtTokenService tokenService) : IAuthService
{
    public async Task<AuthResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Username == request.Username, cancellationToken);
        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return null;
        }

        var token = tokenService.Create(user, request.RememberMe);
        var session = await db.ActiveSessions.FindAsync([user.Id], cancellationToken);
        if (session is null)
        {
            db.ActiveSessions.Add(new ActiveSession { UserId = user.Id, TokenHash = token.Hash, ExpiresAt = token.ExpiresAt });
        }
        else
        {
            session.TokenHash = token.Hash;
            session.CreatedAt = DateTime.UtcNow;
            session.ExpiresAt = token.ExpiresAt;
        }

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new AuthResult(user.Id, user.Username, user.FullName, user.Role, token.Token, token.Hash, token.ExpiresAt);
    }

    public async Task LogoutAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var session = await db.ActiveSessions.FindAsync([userId], cancellationToken);
        if (session is not null)
        {
            db.ActiveSessions.Remove(session);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
