using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using TaskManager.Api.Data;
using Xunit;

namespace TaskManager.Api.Tests.Infrastructure;

/// <summary>
/// Starts SQL Server in Docker and applies the EF Core migrations, which is the
/// path production takes (SQLite tests use EnsureCreated and never run them).
/// </summary>
public sealed class SqlServerApiFactory : ApiFactoryBase, IAsyncLifetime
{
    public const string Image = "mcr.microsoft.com/mssql/server:2022-CU16-ubuntu-22.04";

    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage(Image)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
    }

    protected override void ConfigureDatabase(DbContextOptionsBuilder options) =>
        options.UseSqlServer(ConnectionString);

    protected override void InitializeDatabase(AppDbContext dbContext) =>
        dbContext.Database.Migrate();
}
