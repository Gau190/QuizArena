using System.Security.Claims;
using ExamHub.Core.Interfaces;
using ExamHub.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamHub.Web.Controllers.Api;

[ApiController]
[Route("api/exam")]
[Authorize(Roles = "Student")]
public class ExamApiController(IExamWorkflowService examService) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("start/{examId:int}")]
    public async Task<IActionResult> Start(int examId)
    {
        var attemptId = await examService.StartAttemptAsync(examId, CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(new { attemptId });
    }

    [HttpGet("question/{attemptId:guid}/{index:int}")]
    public async Task<IActionResult> Question(Guid attemptId, int index)
    {
        var payload = await examService.GetQuestionAsync(attemptId, CurrentUserId, index);
        return payload is null ? NotFound() : Ok(payload);
    }

    [HttpPost("answer")]
    public async Task<IActionResult> Answer(SaveAnswerRequest request)
    {
        await examService.SaveAnswerAsync(request, CurrentUserId);
        return NoContent();
    }

    [HttpPost("submit/{attemptId:guid}")]
    public async Task<IActionResult> Submit(Guid attemptId)
    {
        await examService.SubmitAsync(attemptId, CurrentUserId);
        return NoContent();
    }

    [HttpGet("time-remaining/{attemptId:guid}")]
    public async Task<IActionResult> TimeRemaining(Guid attemptId)
    {
        var result = await examService.GetTimeRemainingAsync(attemptId, CurrentUserId);
        return Ok(new { remaining = result.Remaining, autoSubmitted = result.AutoSubmitted });
    }

    [HttpPost("flag")]
    public async Task<IActionResult> Flag(FlagRequest request)
    {
        await examService.FlagAttemptAsync(request, CurrentUserId);
        return NoContent();
    }
}
