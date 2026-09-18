using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using TaskManager.Api.DTOs;
using TaskManager.Api.DTOs.Auth;
using TaskManager.Api.DTOs.Tasks;
using TaskManager.Api.Models;
using Xunit;

namespace TaskManager.Api.Tests;

public sealed class AuthenticationAndOwnershipTests(
    TaskManagerApiFactory factory) : IClassFixture<TaskManagerApiFactory>
{
    private const string ValidPassword = "Portfolio-Test-Password-123!";
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

    [Fact]
    public async Task Tasks_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_SetsSecureHttpOnlyCookie_AndReturnsUser()
    {
        using var client = CreateClient();
        var email = UniqueEmail("cookie");

        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, password = ValidPassword });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));

        var cookie = Assert.Single(cookies);
        Assert.Contains("access_token=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(email, body.User.Email);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        using var registrationClient = CreateClient();
        var email = UniqueEmail("wrong-password");
        await RegisterAsync(registrationClient, email);

        using var loginClient = CreateClient();
        using var response = await loginClient.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "Definitely-Wrong-Password-123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithSameEmailIgnoringCase_ReturnsConflict()
    {
        using var firstClient = CreateClient();
        var localPart = $"case-{Guid.NewGuid():N}";
        await RegisterAsync(firstClient, $"{localPart}@example.com");

        using var secondClient = CreateClient();
        using var response = await secondClient.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = $"{localPart.ToUpperInvariant()}@EXAMPLE.COM",
                password = ValidPassword
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Tasks_AreIsolatedBetweenUsers()
    {
        using var ownerClient = CreateClient();
        using var otherUserClient = CreateClient();
        await RegisterAsync(ownerClient, UniqueEmail("owner"));
        await RegisterAsync(otherUserClient, UniqueEmail("other"));

        using var createResponse = await ownerClient.PostAsJsonAsync(
            "/api/tasks",
            new
            {
                title = "Owner-only task",
                description = "This task must never be visible to another account.",
                isCompleted = false,
                status = "Todo",
                priority = "High",
                dueDate = (DateTime?)null
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);
        Assert.NotNull(createdTask);

        var otherUserTasks = await otherUserClient.GetFromJsonAsync<PagedResult<TaskResponse>>(
            "/api/tasks",
            JsonOptions);
        Assert.NotNull(otherUserTasks);
        Assert.Empty(otherUserTasks.Items);
        Assert.Equal(0, otherUserTasks.TotalCount);

        var otherUserSummary = await otherUserClient.GetFromJsonAsync<TaskSummaryResponse>(
            "/api/tasks/summary",
            JsonOptions);
        Assert.NotNull(otherUserSummary);
        Assert.Equal(0, otherUserSummary.TotalCount);

        using var getResponse = await otherUserClient.GetAsync($"/api/tasks/{createdTask.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        using var putResponse = await otherUserClient.PutAsJsonAsync(
            $"/api/tasks/{createdTask.Id}",
            new
            {
                title = "Unauthorized replacement",
                description = "Another account must not change this task.",
                isCompleted = true,
                status = "Done",
                priority = "Critical",
                dueDate = (DateTime?)null
            });
        Assert.Equal(HttpStatusCode.NotFound, putResponse.StatusCode);

        using var patchRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/tasks/{createdTask.Id}/status")
        {
            Content = JsonContent.Create(new { status = "Done" })
        };
        using var patchResponse = await otherUserClient.SendAsync(patchRequest);
        Assert.Equal(HttpStatusCode.NotFound, patchResponse.StatusCode);

        using var deleteResponse = await otherUserClient.DeleteAsync(
            $"/api/tasks/{createdTask.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        var ownerTask = await ownerClient.GetFromJsonAsync<TaskResponse>(
            $"/api/tasks/{createdTask.Id}",
            JsonOptions);
        Assert.NotNull(ownerTask);
        Assert.Equal("Owner-only task", ownerTask.Title);
        Assert.Equal(TaskItemStatus.Todo, ownerTask.Status);
    }

    private HttpClient CreateClient() => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task RegisterAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static string UniqueEmail(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}@example.com";
}
