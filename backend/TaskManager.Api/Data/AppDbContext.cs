using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Models;

namespace TaskManager.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TaskItem> TaskItems => Set<TaskItem>();

    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(254);

            entity.Property(x => x.PasswordHash)
                .IsRequired()
                .HasMaxLength(512);

            entity.HasIndex(x => x.Email)
                .IsUnique();
        });

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
                .HasDefaultValue(TaskPriority.Medium)
                .HasSentinel((TaskPriority)0);

            entity.HasOne(x => x.User)
                .WithMany(x => x.Tasks)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            var createdAtProperty = entity.Property(x => x.CreatedAt);

            if (Database.IsSqlServer())
            {
                createdAtProperty.HasDefaultValueSql("SYSUTCDATETIME()");
            }

            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.Priority);
            entity.HasIndex(x => x.DueDate);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.Status, x.Priority, x.DueDate });
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
            entity.HasIndex(x => new { x.UserId, x.Status, x.Priority, x.DueDate });
        });
    }
}
