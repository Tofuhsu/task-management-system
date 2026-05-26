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

    public TaskService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<TaskResponse>> SearchAsync(TaskQueryParameters query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, 100);

        var dbQuery = _context.TaskItems.AsNoTracking().AsQueryable();

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

        var totalCount = await dbQuery.CountAsync();

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
            .ToListAsync();

        return new PagedResult<TaskResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<TaskResponse?> GetByIdAsync(int id)
    {
        var task = await _context.TaskItems
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
        {
            return null;
        }

        return ToResponse(task);
    }

    public async Task<TaskResponse> CreateAsync(CreateTaskRequest request)
    {
        var status = ResolveStatus(request.IsCompleted, request.Status);

        var entity = new TaskItem
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            IsCompleted = status == TaskItemStatus.Done,
            Status = status,
            Priority = request.Priority,
            DueDate = request.DueDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };

        _context.TaskItems.Add(entity);
        await _context.SaveChangesAsync();

        return ToResponse(entity);
    }

    public async Task<bool> UpdateAsync(int id, UpdateTaskRequest request)
    {
        var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id);
        if (task == null)
        {
            return false;
        }

        var status = ResolveStatus(request.IsCompleted, request.Status);

        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.IsCompleted = status == TaskItemStatus.Done;
        task.Status = status;
        task.Priority = request.Priority;
        task.DueDate = request.DueDate;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id);
        if (task == null)
        {
            return false;
        }

        _context.TaskItems.Remove(task);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateStatusAsync(int id, UpdateTaskStatusRequest request)
    {
        var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id);
        if (task == null)
        {
            return false;
        }

        task.Status = request.Status;
        task.IsCompleted = request.Status == TaskItemStatus.Done;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<TaskSummaryResponse> GetSummaryAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        return new TaskSummaryResponse
        {
            TotalCount = await _context.TaskItems.CountAsync(),
            TodoCount = await _context.TaskItems.CountAsync(t => t.Status == TaskItemStatus.Todo),
            InProgressCount = await _context.TaskItems.CountAsync(t => t.Status == TaskItemStatus.InProgress),
            DoneCount = await _context.TaskItems.CountAsync(t => t.Status == TaskItemStatus.Done),
            ArchivedCount = await _context.TaskItems.CountAsync(t => t.Status == TaskItemStatus.Archived),
            OverdueCount = await _context.TaskItems.CountAsync(t =>
                t.DueDate != null &&
                t.DueDate < today &&
                t.Status != TaskItemStatus.Done &&
                t.Status != TaskItemStatus.Archived),
            DueTodayCount = await _context.TaskItems.CountAsync(t =>
                t.DueDate != null &&
                t.DueDate >= today &&
                t.DueDate < tomorrow &&
                t.Status != TaskItemStatus.Done &&
                t.Status != TaskItemStatus.Archived)
        };
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