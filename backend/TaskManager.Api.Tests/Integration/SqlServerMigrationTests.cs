using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Api.Data;
using TaskManager.Api.Tests.Infrastructure;
using Xunit;

namespace TaskManager.Api.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class SqlServerMigrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    [Fact]
    public async Task All_migrations_apply_and_the_snapshot_matches_the_model()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());
        Assert.Equal(
            dbContext.Database.GetMigrations(),
            await dbContext.Database.GetAppliedMigrationsAsync());
        Assert.False(
            dbContext.Database.HasPendingModelChanges(),
            "The model has changes that are not captured in a migration.");
    }

    [Fact]
    public async Task Readiness_reports_healthy_against_sql_server()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
