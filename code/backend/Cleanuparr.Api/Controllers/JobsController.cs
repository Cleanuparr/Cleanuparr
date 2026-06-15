using Cleanuparr.Api.Extensions;
using Cleanuparr.Api.Models;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleanuparr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class JobsController : ControllerBase
{
    private readonly IJobManagementService _jobManagementService;
    private readonly ILogger<JobsController> _logger;

    public JobsController(IJobManagementService jobManagementService, ILogger<JobsController> logger)
    {
        _jobManagementService = jobManagementService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllJobs()
    {
        var result = await _jobManagementService.GetAllJobs();
        return Ok(result);
    }

    [HttpGet("{jobType}")]
    public async Task<IActionResult> GetJob(JobType jobType)
    {
        var jobInfo = await _jobManagementService.GetJob(jobType);

        if (jobInfo.Status == "Not Found")
        {
            return this.ProblemResult(StatusCodes.Status404NotFound, $"Job '{jobType}' not found");
        }
        return Ok(jobInfo);
    }

    [HttpPost("{jobType}/start")]
    public async Task<IActionResult> StartJob(JobType jobType, [FromBody] ScheduleRequest scheduleRequest)
    {
        if (jobType == JobType.Seeker)
        {
            return this.ProblemResult(StatusCodes.Status400BadRequest, "The Seeker job cannot be manually controlled");
        }

        // Get the schedule from the request body if provided
        JobSchedule jobSchedule = scheduleRequest.Schedule;

        var result = await _jobManagementService.StartJob(jobType, jobSchedule);

        if (!result)
        {
            return this.ProblemResult(StatusCodes.Status400BadRequest, $"Failed to start job '{jobType}'");
        }
        return Ok(new { Message = $"Job '{jobType}' started successfully" });
    }

    [HttpPost("{jobType}/trigger")]
    public async Task<IActionResult> TriggerJob(JobType jobType)
    {
        if (jobType == JobType.Seeker)
        {
            return this.ProblemResult(StatusCodes.Status400BadRequest, "The Seeker job cannot be manually triggered");
        }

        var result = await _jobManagementService.TriggerJobOnce(jobType);

        if (!result)
        {
            return this.ProblemResult(StatusCodes.Status400BadRequest, $"Failed to trigger job '{jobType}' - job may not exist or be configured");
        }
        return Ok(new { Message = $"Job '{jobType}' triggered successfully for one-time execution" });
    }

    [HttpPut("{jobType}/schedule")]
    public async Task<IActionResult> UpdateJobSchedule(JobType jobType, [FromBody] ScheduleRequest scheduleRequest)
    {
        if (jobType == JobType.Seeker)
        {
            return this.ProblemResult(StatusCodes.Status400BadRequest, "The Seeker job schedule cannot be manually modified");
        }

        if (scheduleRequest?.Schedule == null)
        {
            return this.ProblemResult(StatusCodes.Status400BadRequest, "Schedule is required");
        }

        var result = await _jobManagementService.UpdateJobSchedule(jobType, scheduleRequest.Schedule);

        if (!result)
        {
            return this.ProblemResult(StatusCodes.Status400BadRequest, $"Failed to update schedule for job '{jobType}'");
        }
        return Ok(new { Message = $"Job '{jobType}' schedule updated successfully" });
    }
}
