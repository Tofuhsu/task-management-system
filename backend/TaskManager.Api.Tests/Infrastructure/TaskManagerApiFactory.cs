using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;

namespace TaskManager.Api.Tests.Infrastructure;

/// <summary>Uses a unique temporary SQLite file, created with EnsureCreated.</summary>
public sealed class TaskManagerApiFactory : ApiFactoryBase
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"taskmanager-integration-{Guid.NewGuid():N}.db");

    protected override void ConfigureDatabase(DbContextOptionsBuilder options) =>
        options.UseSqlite($"Data Source={_databasePath}");

    protected override void InitializeDatabase(AppDbContext dbContext) =>
        dbContext.Database.EnsureCreated();

    protected override void Dispose(bool disposing)
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

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
