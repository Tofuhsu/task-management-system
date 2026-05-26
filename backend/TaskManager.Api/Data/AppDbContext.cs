using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Models;

namespace TaskManager.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TaskItem> TaskItems => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Description)
                .HasMaxLength(2000);

            entity.Property(x => x.IsCompleted)
                .HasDefaultValue(false);

            entity.Property(x => x.Status)
                .HasDefaultValue(TaskItemStatus.Todo);

            entity.Property(x => x.Priority)
                .HasDefaultValue(TaskPriority.Medium);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.Priority);
            entity.HasIndex(x => x.DueDate);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.Status, x.Priority, x.DueDate });
        });
    }
}