using TaskManager.Api.Models;

namespace TaskManager.Api.DTOs.Tasks;

public class TaskQueryParameters
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public string? Search { get; set; }

    public TaskItemStatus? Status { get; set; }

    public TaskPriority? Priority { get; set; }

    public DateTime? DueFrom { get; set; }

    public DateTime? DueTo { get; set; }

    public string SortBy { get; set; } = "CreatedAt";

    public string SortDirection { get; set; } = "desc";
}