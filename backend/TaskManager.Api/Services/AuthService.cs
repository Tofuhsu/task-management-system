using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.DTOs.Auth;
using TaskManager.Api.Models;
using TaskManager.Api.Services.Interfaces;

namespace TaskManager.Api.Services;

public sealed class AuthService(
    AppDbContext context,
    IPasswordHasher<AppUser> passwordHasher,
    IJwtTokenService jwtTokenService,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResult?> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);

        if (await context.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            return null;
        }

        var user = new AppUser
        {
            Email = email,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        context.Users.Add(user);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(
                exception,
                "Registration failed because the normalized email already exists");
            return null;
        }

        logger.LogInformation("Registered user {UserId}", user.Id);
        return CreateResult(user);
    }

    public async Task<AuthResult?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await context.Users.SingleOrDefaultAsync(
            candidate => candidate.Email == email,
            cancellationToken);

        if (user is null)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await context.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Authenticated user {UserId}", user.Id);
        return CreateResult(user);
    }

    public async Task<UserResponse?> GetUserAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        return await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new UserResponse(user.Id, user.Email))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private AuthResult CreateResult(AppUser user)
    {
        var accessToken = jwtTokenService.Create(user);
        return new AuthResult(
            accessToken.Value,
            accessToken.ExpiresAt,
            new UserResponse(user.Id, user.Email));
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();
}
