using TaskManager.Api.DTOs;
using TaskManager.Api.DTOs.Tasks;

namespace TaskManager.Api.Services.Interfaces;

public interface ITaskService
{
    Task<PagedResult<TaskResponse>> SearchAsync(TaskQueryParameters query);
    Task<TaskResponse?> GetByIdAsync(int id);
    Task<TaskResponse> CreateAsync(CreateTaskRequest request);
    Task<bool> UpdateAsync(int id, UpdateTaskRequest request);
    Task<bool> DeleteAsync(int id);
    Task<bool> UpdateStatusAsync(int id, UpdateTaskStatusRequest request);
    Task<TaskSummaryResponse> GetSummaryAsync();
}