using System.Net;
using Xunit;

namespace TaskManager.Api.Tests;

public sealed class HealthCheckTests(TaskManagerApiFactory factory)
    : IClassFixture<TaskManagerApiFactory>
{
    [Fact]
    public async Task Health_returns_ok_when_database_is_available()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
