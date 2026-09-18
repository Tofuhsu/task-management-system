using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManager.Api.DTOs;
using TaskManager.Api.DTOs.Tasks;
using TaskManager.Api.Services.Interfaces;

namespace TaskManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TasksController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TaskResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<TaskResponse>>> GetTasks(
        [FromQuery] TaskQueryParameters query,
        CancellationToken cancellationToken)
    {
        var result = await _taskService.SearchAsync(
            GetUserId(),
            query,
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> GetTaskById(
        int id,
        CancellationToken cancellationToken)
    {
        var task = await _taskService.GetByIdAsync(
            GetUserId(),
            id,
            cancellationToken);
        if (task == null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Task not found.",
                detail: $"Task {id} does not exist.");
        }

        return Ok(task);
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(TaskSummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskSummaryResponse>> GetSummary(
        CancellationToken cancellationToken)
    {
        var summary = await _taskService.GetSummaryAsync(
            GetUserId(),
            cancellationToken);
        return Ok(summary);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaskResponse>> CreateTask(
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _taskService.CreateAsync(
            GetUserId(),
            request,
            cancellationToken);
        return CreatedAtAction(nameof(GetTaskById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTask(
        int id,
        [FromBody] UpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _taskService.UpdateAsync(
            GetUserId(),
            id,
            request,
            cancellationToken);
        return updated
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Task not found.",
                detail: $"Task {id} does not exist.");
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _taskService.UpdateStatusAsync(
            GetUserId(),
            id,
            request,
            cancellationToken);
        return updated
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Task not found.",
                detail: $"Task {id} does not exist.");
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTask(
        int id,
        CancellationToken cancellationToken)
    {
        var deleted = await _taskService.DeleteAsync(
            GetUserId(),
            id,
            cancellationToken);
        return deleted
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Task not found.",
                detail: $"Task {id} does not exist.");
    }

    private int GetUserId()
    {
        var claim = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (int.TryParse(claim, out var userId))
        {
            return userId;
        }

        throw new InvalidOperationException(
            "The authenticated principal does not contain a valid subject claim.");
    }
}
