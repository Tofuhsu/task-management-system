using TaskManager.Api.Models;

namespace TaskManager.Api.DTOs.Tasks;

public class TaskSummaryResponse
{
    public int TotalCount { get; set; }
    public int TodoCount { get; set; }
    public int InProgressCount { get; set; }
    public int DoneCount { get; set; }
    public int ArchivedCount { get; set; }
    public int OverdueCount { get; set; }
    public int DueTodayCount { get; set; }
}