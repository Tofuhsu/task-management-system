using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TaskManager.Api.Data;
using TaskManager.Api.DTOs.Tasks;
using TaskManager.Api.Models;
using TaskManager.Api.Services;
using Xunit;

namespace TaskManager.Api.Tests.Unit;

/// <summary>
/// Exercises TaskService directly against an in-memory SQLite database, so the
/// LINQ is translated by a real relational provider.
/// </summary>
public sealed class TaskServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly AppDbContext _context;
    private readonly TaskService _service;
    private readonly int _userId;
    private readonly int _otherUserId;

    public TaskServiceTests()
    {
        _connection.Open();
        _context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        var user = new AppUser { Email = "owner@example.com", PasswordHash = "x" };
        var other = new AppUser { Email = "other@example.com", PasswordHash = "x" };
        _context.Users.AddRange(user, other);
        _context.SaveChanges();
        _userId = user.Id;
        _otherUserId = other.Id;

        _service = new TaskService(_context, NullLogger<TaskService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Theory]
    [InlineData(false, null, TaskItemStatus.Todo, false)]
    [InlineData(true, null, TaskItemStatus.Done, true)]
    [InlineData(true, TaskItemStatus.InProgress, TaskItemStatus.InProgress, false)]
    [InlineData(false, TaskItemStatus.Done, TaskItemStatus.Done, true)]
    public async Task Create_resolves_status_and_keeps_IsCompleted_consistent(
        bool isCompleted,
        TaskItemStatus? requestedStatus,
        TaskItemStatus expectedStatus,
        bool expectedCompleted)
    {
        var created = await _service.CreateAsync(
            _userId,
            new CreateTaskRequest { Title = "t", IsCompleted = isCompleted, Status = requestedStatus },
            CancellationToken.None);

        Assert.Equal(expectedStatus, created.Status);
        Assert.Equal(expectedCompleted, created.IsCompleted);
    }

    [Fact]
    public async Task Create_trims_title_and_turns_blank_description_into_null()
    {
        var created = await _service.CreateAsync(
            _userId,
            new CreateTaskRequest { Title = "  Write report  ", Description = "   " },
            CancellationToken.None);

        Assert.Equal("Write report", created.Title);
        Assert.Null(created.Description);
    }

    [Fact]
    public async Task Search_matches_title_or_description_and_only_returns_own_tasks()
    {
        await AddTaskAsync(_userId, "Backend API", description: null);
        await AddTaskAsync(_userId, "Groceries", description: "buy backend snacks");
        await AddTaskAsync(_userId, "Unrelated", description: "nothing");
        await AddTaskAsync(_otherUserId, "Backend for someone else", description: null);

        var result = await _service.SearchAsync(
            _userId,
            new TaskQueryParameters { Search = "  backend ", SortBy = "Title", SortDirection = "asc" },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(["Backend API", "Groceries"], result.Items.Select(t => t.Title));
    }

    [Fact]
    public async Task Search_pages_results_and_computes_total_pages()
    {
        for (var i = 0; i < 7; i++)
        {
            await AddTaskAsync(_userId, $"Task {i}", createdAt: new DateTime(2026, 1, 1).AddDays(i));
        }

        var page = await _service.SearchAsync(
            _userId,
            new TaskQueryParameters { Page = 3, PageSize = 3 },
            CancellationToken.None);

        Assert.Equal(7, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        // Default order is CreatedAt descending, so the last page holds the oldest task.
        Assert.Equal(["Task 0"], page.Items.Select(t => t.Title));
    }

    [Fact]
    public async Task Search_sorts_by_priority_descending_then_newest_first()
    {
        await AddTaskAsync(_userId, "low", priority: TaskPriority.Low, createdAt: new DateTime(2026, 1, 1));
        await AddTaskAsync(_userId, "high-old", priority: TaskPriority.High, createdAt: new DateTime(2026, 1, 2));
        await AddTaskAsync(_userId, "high-new", priority: TaskPriority.High, createdAt: new DateTime(2026, 1, 3));
        await AddTaskAsync(_userId, "critical", priority: TaskPriority.Critical, createdAt: new DateTime(2026, 1, 4));

        var result = await _service.SearchAsync(
            _userId,
            new TaskQueryParameters { SortBy = "priority", SortDirection = "DESC" },
            CancellationToken.None);

        Assert.Equal(["critical", "high-new", "high-old", "low"], result.Items.Select(t => t.Title));
    }

    [Fact]
    public async Task Search_filters_by_status_priority_and_due_range()
    {
        var from = new DateTime(2026, 3, 1);
        await AddTaskAsync(_userId, "match", TaskItemStatus.Todo, TaskPriority.High, due: from.AddDays(1));
        await AddTaskAsync(_userId, "wrong-status", TaskItemStatus.Done, TaskPriority.High, due: from.AddDays(1));
        await AddTaskAsync(_userId, "wrong-priority", TaskItemStatus.Todo, TaskPriority.Low, due: from.AddDays(1));
        await AddTaskAsync(_userId, "too-late", TaskItemStatus.Todo, TaskPriority.High, due: from.AddDays(30));
        await AddTaskAsync(_userId, "no-due", TaskItemStatus.Todo, TaskPriority.High, due: null);

        var result = await _service.SearchAsync(
            _userId,
            new TaskQueryParameters
            {
                Status = TaskItemStatus.Todo,
                Priority = TaskPriority.High,
                DueFrom = from,
                DueTo = from.AddDays(7)
            },
            CancellationToken.None);

        Assert.Equal("match", Assert.Single(result.Items).Title);
    }

    [Fact]
    public async Task Summary_counts_overdue_and_due_today_only_for_open_tasks()
    {
        var today = DateTime.UtcNow.Date;
        await AddTaskAsync(_userId, "overdue", TaskItemStatus.Todo, due: today.AddDays(-1));
        await AddTaskAsync(_userId, "overdue-but-done", TaskItemStatus.Done, due: today.AddDays(-1));
        await AddTaskAsync(_userId, "due-today", TaskItemStatus.InProgress, due: today.AddHours(12));
        await AddTaskAsync(_userId, "due-today-archived", TaskItemStatus.Archived, due: today.AddHours(1));
        await AddTaskAsync(_userId, "due-tomorrow", TaskItemStatus.Todo, due: today.AddDays(1));
        await AddTaskAsync(_otherUserId, "someone-else-overdue", TaskItemStatus.Todo, due: today.AddDays(-3));

        var summary = await _service.GetSummaryAsync(_userId, CancellationToken.None);

        Assert.Equal(5, summary.TotalCount);
        Assert.Equal(2, summary.TodoCount);
        Assert.Equal(1, summary.InProgressCount);
        Assert.Equal(1, summary.DoneCount);
        Assert.Equal(1, summary.ArchivedCount);
        Assert.Equal(1, summary.OverdueCount);
        Assert.Equal(1, summary.DueTodayCount);
    }

    [Fact]
    public async Task Summary_for_user_without_tasks_is_all_zero()
    {
        var summary = await _service.GetSummaryAsync(_userId, CancellationToken.None);

        Assert.Equal(0, summary.TotalCount);
        Assert.Equal(0, summary.OverdueCount);
    }

    [Fact]
    public async Task Status_update_sets_IsCompleted_and_UpdatedAt()
    {
        var task = await AddTaskAsync(_userId, "t");

        var updated = await _service.UpdateStatusAsync(
            _userId,
            task.Id,
            new UpdateTaskStatusRequest { Status = TaskItemStatus.Done },
            CancellationToken.None);

        Assert.True(updated);
        var reloaded = await _service.GetByIdAsync(_userId, task.Id, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.True(reloaded.IsCompleted);
        Assert.NotNull(reloaded.UpdatedAt);
    }

    [Fact]
    public async Task Writes_to_another_users_task_report_not_found()
    {
        var task = await AddTaskAsync(_otherUserId, "not yours");

        Assert.False(await _service.UpdateStatusAsync(
            _userId, task.Id, new UpdateTaskStatusRequest { Status = TaskItemStatus.Done }, CancellationToken.None));
        Assert.False(await _service.DeleteAsync(_userId, task.Id, CancellationToken.None));
        Assert.Null(await _service.GetByIdAsync(_userId, task.Id, CancellationToken.None));
    }

    private async Task<TaskItem> AddTaskAsync(
        int userId,
        string title,
        TaskItemStatus status = TaskItemStatus.Todo,
        TaskPriority priority = TaskPriority.Medium,
        DateTime? due = null,
        string? description = null,
        DateTime? createdAt = null)
    {
        var task = new TaskItem
        {
            UserId = userId,
            Title = title,
            Description = description,
            Status = status,
            IsCompleted = status == TaskItemStatus.Done,
            Priority = priority,
            DueDate = due,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        _context.TaskItems.Add(task);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return task;
    }
}
