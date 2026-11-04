using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TaskManager.Api.Configuration;
using TaskManager.Api.Models;
using TaskManager.Api.Services;
using Xunit;

namespace TaskManager.Api.Tests.Unit;

public sealed class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "issuer.test",
        Audience = "audience.test",
        SigningKey = "unit-test-signing-key-that-is-longer-than-32-chars",
        ExpirationMinutes = 15
    };

    [Fact]
    public void Token_carries_subject_email_and_unique_id_and_validates()
    {
        var service = new JwtTokenService(Options);
        var user = new AppUser { Id = 42, Email = "user@example.com" };

        var first = service.Create(user);
        var second = service.Create(user);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(first.Value, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.SigningKey)),
            ClockSkew = TimeSpan.Zero
        }, out var validated);

        Assert.Equal("42", principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal("user@example.com", principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value);
        Assert.Equal(SecurityAlgorithms.HmacSha256, ((JwtSecurityToken)validated).Header.Alg);
        Assert.NotEqual(
            handler.ReadJwtToken(first.Value).Id,
            handler.ReadJwtToken(second.Value).Id);
    }

    [Fact]
    public void Expiry_matches_configured_lifetime()
    {
        var before = DateTime.UtcNow;
        var token = new JwtTokenService(Options).Create(new AppUser { Id = 1, Email = "a@b.c" });
        var after = DateTime.UtcNow;

        Assert.InRange(
            token.ExpiresAt,
            before.AddMinutes(Options.ExpirationMinutes),
            after.AddMinutes(Options.ExpirationMinutes));
        var jwtExpiry = new JwtSecurityTokenHandler().ReadJwtToken(token.Value).ValidTo;
        Assert.True(Math.Abs((jwtExpiry - token.ExpiresAt).TotalSeconds) < 1);
    }

    [Fact]
    public void Token_signed_with_another_key_is_rejected()
    {
        var token = new JwtTokenService(Options).Create(new AppUser { Id = 1, Email = "a@b.c" });

        Assert.ThrowsAny<SecurityTokenException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(token.Value, new TokenValidationParameters
            {
                ValidIssuer = Options.Issuer,
                ValidAudience = Options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes("a-completely-different-signing-key-of-32+-chars"))
            }, out _));
    }
}
