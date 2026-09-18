using System.ComponentModel.DataAnnotations;
using TaskManager.Api.Models;

namespace TaskManager.Api.DTOs.Tasks;

public class TaskQueryParameters : IValidatableObject
{
    private static readonly string[] AllowedSortFields =
        ["CreatedAt", "Title", "DueDate", "Priority", "Status"];

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    [StringLength(200)]
    public string? Search { get; set; }

    [EnumDataType(typeof(TaskItemStatus))]
    public TaskItemStatus? Status { get; set; }

    [EnumDataType(typeof(TaskPriority))]
    public TaskPriority? Priority { get; set; }

    public DateTime? DueFrom { get; set; }

    public DateTime? DueTo { get; set; }

    public string SortBy { get; set; } = "CreatedAt";

    public string SortDirection { get; set; } = "desc";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DueFrom.HasValue && DueTo.HasValue && DueFrom > DueTo)
        {
            yield return new ValidationResult(
                "DueFrom must be earlier than or equal to DueTo.",
                [nameof(DueFrom), nameof(DueTo)]);
        }

        if (!AllowedSortFields.Contains(SortBy, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                $"SortBy must be one of: {string.Join(", ", AllowedSortFields)}.",
                [nameof(SortBy)]);
        }

        if (!string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "SortDirection must be either 'asc' or 'desc'.",
                [nameof(SortDirection)]);
        }
    }
}
