using TaskManager.Api.DTOs.Auth;

namespace TaskManager.Api.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResult?> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken);

    Task<AuthResult?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken);

    Task<UserResponse?> GetUserAsync(int userId, CancellationToken cancellationToken);
}

public sealed record AuthResult(string AccessToken, DateTime ExpiresAt, UserResponse User);
