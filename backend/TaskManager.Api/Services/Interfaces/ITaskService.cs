using TaskManager.Api.DTOs;
using TaskManager.Api.DTOs.Tasks;

namespace TaskManager.Api.Services.Interfaces;

public interface ITaskService
{
    Task<PagedResult<TaskResponse>> SearchAsync(
        int userId,
        TaskQueryParameters query,
        CancellationToken cancellationToken);

    Task<TaskResponse?> GetByIdAsync(
        int userId,
        int id,
        CancellationToken cancellationToken);

    Task<TaskResponse> CreateAsync(
        int userId,
        CreateTaskRequest request,
        CancellationToken cancellationToken);

    Task<bool> UpdateAsync(
        int userId,
        int id,
        UpdateTaskRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(
        int userId,
        int id,
        CancellationToken cancellationToken);

    Task<bool> UpdateStatusAsync(
        int userId,
        int id,
        UpdateTaskStatusRequest request,
        CancellationToken cancellationToken);

    Task<TaskSummaryResponse> GetSummaryAsync(
        int userId,
        CancellationToken cancellationToken);
}
