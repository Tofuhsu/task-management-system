using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.DTOs;
using TaskManager.Api.DTOs.Tasks;
using TaskManager.Api.Models;
using TaskManager.Api.Services.Interfaces;

namespace TaskManager.Api.Services;

public class TaskService : ITaskService
{
    private readonly AppDbContext _context;
    private readonly ILogger<TaskService> _logger;

    public TaskService(AppDbContext context, ILogger<TaskService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<TaskResponse>> SearchAsync(
        int userId,
        TaskQueryParameters query,
        CancellationToken cancellationToken)
    {
        var page = query.Page;
        var pageSize = query.PageSize;

        var dbQuery = _context.TaskItems
            .AsNoTracking()
            .Where(task => task.UserId == userId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var keyword = query.Search.Trim();
            dbQuery = dbQuery.Where(t =>
                EF.Functions.Like(t.Title, $"%{keyword}%") ||
                (t.Description != null && EF.Functions.Like(t.Description, $"%{keyword}%")));
        }

        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(t => t.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            dbQuery = dbQuery.Where(t => t.Priority == query.Priority.Value);
        }

        if (query.DueFrom.HasValue)
        {
            dbQuery = dbQuery.Where(t => t.DueDate >= query.DueFrom.Value);
        }

        if (query.DueTo.HasValue)
        {
            dbQuery = dbQuery.Where(t => t.DueDate <= query.DueTo.Value);
        }

        dbQuery = ApplySorting(dbQuery, query.SortBy, query.SortDirection);

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        var items = await dbQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TaskResponse
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                IsCompleted = t.IsCompleted,
                Status = t.Status,
                Priority = t.Priority,
                DueDate = t.DueDate,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<TaskResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<TaskResponse?> GetByIdAsync(
        int userId,
        int id,
        CancellationToken cancellationToken)
    {
        var task = await _context.TaskItems
            .AsNoTracking()
            .FirstOrDefaultAsync(
                task => task.Id == id && task.UserId == userId,
                cancellationToken);

        if (task == null)
        {
            return null;
        }

        return ToResponse(task);
    }

    public async Task<TaskResponse> CreateAsync(
        int userId,
        CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var status = ResolveStatus(request.IsCompleted, request.Status);

        var entity = new TaskItem
        {
            UserId = userId,
            Title = request.Title.Trim(),
            Description = NormalizeOptionalText(request.Description),
            IsCompleted = status == TaskItemStatus.Done,
            Status = status,
            Priority = request.Priority,
            DueDate = request.DueDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };

        _context.TaskItems.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created task {TaskId} for user {UserId} with status {Status} and priority {Priority}",
            entity.Id,
            userId,
            entity.Status,
            entity.Priority);

        return ToResponse(entity);
    }

    public async Task<bool> UpdateAsync(
        int userId,
        int id,
        UpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var task = await _context.TaskItems.FirstOrDefaultAsync(
            candidate => candidate.Id == id && candidate.UserId == userId,
            cancellationToken);
        if (task == null)
        {
            return false;
        }

        var status = ResolveStatus(request.IsCompleted, request.Status);

        task.Title = request.Title.Trim();
        task.Description = NormalizeOptionalText(request.Description);
        task.IsCompleted = status == TaskItemStatus.Done;
        task.Status = status;
        task.Priority = request.Priority;
        task.DueDate = request.DueDate;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated task {TaskId} for user {UserId} with status {Status} and priority {Priority}",
            task.Id,
            userId,
            task.Status,
            task.Priority);

        return true;
    }

    public async Task<bool> DeleteAsync(
        int userId,
        int id,
        CancellationToken cancellationToken)
    {
        var task = await _context.TaskItems.FirstOrDefaultAsync(
            candidate => candidate.Id == id && candidate.UserId == userId,
            cancellationToken);
        if (task == null)
        {
            return false;
        }

        _context.TaskItems.Remove(task);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted task {TaskId} for user {UserId}",
            task.Id,
            userId);

        return true;
    }

    public async Task<bool> UpdateStatusAsync(
        int userId,
        int id,
        UpdateTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var task = await _context.TaskItems.FirstOrDefaultAsync(
            candidate => candidate.Id == id && candidate.UserId == userId,
            cancellationToken);
        if (task == null)
        {
            return false;
        }

        var status = request.Status!.Value;

        task.Status = status;
        task.IsCompleted = status == TaskItemStatus.Done;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Changed task {TaskId} status to {Status} for user {UserId}",
            task.Id,
            task.Status,
            userId);

        return true;
    }

    public async Task<TaskSummaryResponse> GetSummaryAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var summary = await _context.TaskItems
            .AsNoTracking()
            .Where(task => task.UserId == userId)
            .GroupBy(_ => 1)
            .Select(group => new TaskSummaryResponse
            {
                TotalCount = group.Count(),
                TodoCount = group.Count(t => t.Status == TaskItemStatus.Todo),
                InProgressCount = group.Count(t => t.Status == TaskItemStatus.InProgress),
                DoneCount = group.Count(t => t.Status == TaskItemStatus.Done),
                ArchivedCount = group.Count(t => t.Status == TaskItemStatus.Archived),
                OverdueCount = group.Count(t =>
                    t.DueDate != null &&
                    t.DueDate < today &&
                    t.Status != TaskItemStatus.Done &&
                    t.Status != TaskItemStatus.Archived),
                DueTodayCount = group.Count(t =>
                    t.DueDate != null &&
                    t.DueDate >= today &&
                    t.DueDate < tomorrow &&
                    t.Status != TaskItemStatus.Done &&
                    t.Status != TaskItemStatus.Archived)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return summary ?? new TaskSummaryResponse();
    }

    private static IQueryable<TaskItem> ApplySorting(
        IQueryable<TaskItem> query,
        string sortBy,
        string sortDirection)
    {
        var desc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        var field = (sortBy ?? string.Empty).Trim().ToLowerInvariant();

        return field switch
        {
            "title" => desc
                ? query.OrderByDescending(x => x.Title).ThenByDescending(x => x.CreatedAt)
                : query.OrderBy(x => x.Title).ThenByDescending(x => x.CreatedAt),

            "duedate" => desc
                ? query.OrderByDescending(x => x.DueDate).ThenByDescending(x => x.CreatedAt)
                : query.OrderBy(x => x.DueDate).ThenByDescending(x => x.CreatedAt),

            "priority" => desc
                ? query.OrderByDescending(x => x.Priority).ThenByDescending(x => x.CreatedAt)
                : query.OrderBy(x => x.Priority).ThenByDescending(x => x.CreatedAt),

            "status" => desc
                ? query.OrderByDescending(x => x.Status).ThenByDescending(x => x.CreatedAt)
                : query.OrderBy(x => x.Status).ThenByDescending(x => x.CreatedAt),

            _ => desc
                ? query.OrderByDescending(x => x.CreatedAt)
                : query.OrderBy(x => x.CreatedAt)
        };
    }

    private static TaskItemStatus ResolveStatus(bool isCompleted, TaskItemStatus? status)
    {
        if (status.HasValue)
        {
            return status.Value;
        }

        return isCompleted ? TaskItemStatus.Done : TaskItemStatus.Todo;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static TaskResponse ToResponse(TaskItem task)
    {
        return new TaskResponse
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            IsCompleted = task.IsCompleted,
            Status = task.Status,
            Priority = task.Priority,
            DueDate = task.DueDate,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt
        };
    }
}
