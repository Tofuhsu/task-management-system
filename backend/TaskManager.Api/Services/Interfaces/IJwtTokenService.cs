using TaskManager.Api.Models;

namespace TaskManager.Api.Services.Interfaces;

public interface IJwtTokenService
{
    AccessToken Create(AppUser user);
}

public sealed record AccessToken(string Value, DateTime ExpiresAt);
