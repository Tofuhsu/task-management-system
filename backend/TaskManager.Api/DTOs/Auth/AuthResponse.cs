namespace TaskManager.Api.DTOs.Auth;

public sealed record AuthResponse(UserResponse User, DateTime ExpiresAt);
