using System.Net;
using System.Text.Json;
using TaskManager.Api.Tests.Infrastructure;
using Xunit;

namespace TaskManager.Api.Tests.Integration;

public sealed class HealthCheckTests(TaskManagerApiFactory factory)
    : IClassFixture<TaskManagerApiFactory>
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task Readiness_returns_ok_and_reports_database_check(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        var check = Assert.Single(body.RootElement.GetProperty("checks").EnumerateArray());
        Assert.Equal("database", check.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Liveness_returns_ok_without_running_dependency_checks()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(body.RootElement.GetProperty("checks").EnumerateArray());
    }
}
