using System.ComponentModel.DataAnnotations;
using TaskManager.Api.Models;

namespace TaskManager.Api.DTOs.Tasks;

public class UpdateTaskStatusRequest
{
    [Required]
    public TaskItemStatus Status { get; set; }
}