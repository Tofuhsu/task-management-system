using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskManager.Api.Data;

namespace TaskManager.Api.Tests;

public sealed class TaskManagerApiFactory : WebApplicationFactory<Program>
{
    private static readonly IReadOnlyDictionary<string, string> TestEnvironmentVariables =
        new Dictionary<string, string>
        {
            ["Jwt__Issuer"] = "TaskManager.Api.Tests",
            ["Jwt__Audience"] = "TaskManager.Web.Tests",
            ["Jwt__SigningKey"] =
                "integration-test-only-signing-key-with-more-than-32-characters",
            ["Jwt__ExpirationMinutes"] = "60"
        };

    private readonly IReadOnlyDictionary<string, string?> _originalEnvironmentVariables;
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"taskmanager-integration-{Guid.NewGuid():N}.db");

    public TaskManagerApiFactory()
    {
        _originalEnvironmentVariables = TestEnvironmentVariables.Keys.ToDictionary(
            key => key,
            Environment.GetEnvironmentVariable);

        foreach (var (key, value) in TestEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={_databasePath}"));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);

            if (!disposing)
            {
                return;
            }

            SqliteConnection.ClearAllPools();
            DeleteIfExists(_databasePath);
            DeleteIfExists($"{_databasePath}-wal");
            DeleteIfExists($"{_databasePath}-shm");
        }
        finally
        {
            if (disposing)
            {
                RestoreEnvironmentVariables();
            }
        }
    }

    private void RestoreEnvironmentVariables()
    {
        foreach (var (key, value) in _originalEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
