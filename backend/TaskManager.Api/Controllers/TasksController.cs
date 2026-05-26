using Microsoft.AspNetCore.Mvc;
using TaskManager.Api.DTOs.Tasks;
using TaskManager.Api.Services.Interfaces;

namespace TaskManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TasksController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet]
    public async Task<ActionResult> GetTasks([FromQuery] TaskQueryParameters query)
    {
        var result = await _taskService.SearchAsync(query);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult> GetTaskById(int id)
    {
        var task = await _taskService.GetByIdAsync(id);
        if (task == null)
        {
            return NotFound();
        }

        return Ok(task);
    }

    [HttpGet("summary")]
    public async Task<ActionResult> GetSummary()
    {
        var summary = await _taskService.GetSummaryAsync();
        return Ok(summary);
    }

    [HttpPost]
    public async Task<ActionResult> CreateTask([FromBody] CreateTaskRequest request)
    {
        var created = await _taskService.CreateAsync(request);
        return CreatedAtAction(nameof(GetTaskById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTask(int id, [FromBody] UpdateTaskRequest request)
    {
        var updated = await _taskService.UpdateAsync(id, request);
        return updated ? NoContent() : NotFound();
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateTaskStatusRequest request)
    {
        var updated = await _taskService.UpdateStatusAsync(id, request);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var deleted = await _taskService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}