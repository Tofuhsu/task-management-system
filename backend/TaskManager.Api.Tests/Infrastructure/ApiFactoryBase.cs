using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskManager.Api.Data;

namespace TaskManager.Api.Tests.Infrastructure;

/// <summary>
/// Hosts the real application in memory with test JWT settings and a database
/// chosen by the derived factory.
/// </summary>
public abstract class ApiFactoryBase : WebApplicationFactory<Program>
{
    private static readonly IReadOnlyDictionary<string, string> TestEnvironmentVariables =
        new Dictionary<string, string>
        {
            ["Jwt__Issuer"] = "TaskManager.Api.Tests",
            ["Jwt__Audience"] = "TaskManager.Web.Tests",
            ["Jwt__SigningKey"] =
                "integration-test-only-signing-key-with-more-than-32-characters",
            ["Jwt__ExpirationMinutes"] = "60",
            // Shared test hosts register many users from one (null) client IP; the
            // rate-limiting tests lower this explicitly.
            ["RateLimiting__Auth__PermitLimit"] = "1000"
        };

    private readonly IReadOnlyDictionary<string, string?> _originalEnvironmentVariables;

    protected ApiFactoryBase()
    {
        _originalEnvironmentVariables = TestEnvironmentVariables.Keys.ToDictionary(
            key => key,
            Environment.GetEnvironmentVariable);

        foreach (var (key, value) in TestEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    protected abstract void ConfigureDatabase(DbContextOptionsBuilder options);

    protected abstract void InitializeDatabase(AppDbContext dbContext);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(ConfigureDatabase);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        InitializeDatabase(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
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
}
