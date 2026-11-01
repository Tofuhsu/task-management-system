using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Api.Configuration;
using Xunit;

namespace TaskManager.Api.Tests;

public sealed class RateLimitingTests(TaskManagerApiFactory factory)
    : IClassFixture<TaskManagerApiFactory>
{
    private const int PermitLimit = 3;
    private const string ValidPassword = "Portfolio-Test-Password-123!";

    [Fact]
    public async Task Credential_attempts_over_the_limit_return_429_with_retry_after()
    {
        using var host = CreateLimitedHost();
        using var client = CreateClient(host);
        var email = UniqueEmail();

        using (var register = await PostCredentialsAsync(client, "register", email, ValidPassword))
        {
            Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        }

        // Failed logins consume the same budget as successful ones.
        for (var attempt = 2; attempt <= PermitLimit; attempt++)
        {
            using var wrong = await PostCredentialsAsync(client, "login", email, "Wrong-Password-123!");
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        using var limited = await PostCredentialsAsync(client, "login", email, ValidPassword);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);

        using var problem = JsonDocument.Parse(await limited.Content.ReadAsStringAsync());
        Assert.Equal(429, problem.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Session_endpoints_are_not_limited_by_the_credential_policy()
    {
        using var host = CreateLimitedHost();
        using var client = CreateClient(host);
        var email = UniqueEmail();

        using (var register = await PostCredentialsAsync(client, "register", email, ValidPassword))
        {
            Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        }

        for (var attempt = 2; attempt <= PermitLimit + 1; attempt++)
        {
            using var _ = await PostCredentialsAsync(client, "login", email, "Wrong-Password-123!");
        }

        using (var throttled = await PostCredentialsAsync(client, "login", email, ValidPassword))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        }

        for (var request = 0; request < PermitLimit * 3; request++)
        {
            using var me = await client.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }
    }

    private WebApplicationFactory<Program> CreateLimitedHost() =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.PostConfigure<AuthRateLimitOptions>(options =>
                {
                    options.PermitLimit = PermitLimit;
                    options.WindowSeconds = 60;
                })));

    private static HttpClient CreateClient(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static Task<HttpResponseMessage> PostCredentialsAsync(
        HttpClient client,
        string action,
        string email,
        string password) =>
        client.PostAsJsonAsync($"/api/auth/{action}", new { email, password });

    private static string UniqueEmail() => $"limit-{Guid.NewGuid():N}@example.com";
}
