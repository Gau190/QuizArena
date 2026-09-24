using System.Security.Claims;
using QuizArena.Core.Enums;
using QuizArena.Core.Interfaces;
using QuizArena.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuizArena.Web.Controllers.Api;

// Payload sent to the examinee: never includes IsCorrect / Explanation.
public record ChoiceView(int Id, string Content);

public record QuestionView(
    int Index,
    int Total,
    int QuestionId,
    string Content,
    string Type,
    decimal Points,
    IReadOnlyList<ChoiceView> Choices,
    string? SelectedAnswerIds,
    string? TextInput);

[ApiController]
[Route("api/v1/exam")]
[Authorize(Roles = "Student")]
public class ExamApiController(IExamWorkflowService examService) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("start/{examId:int}")]
    public async Task<IActionResult> Start(int examId)
    {
        try
        {
            var attemptId = await examService.StartAttemptAsync(examId, CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(new { attemptId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("question/{attemptId:guid}/{index:int}")]
    public async Task<IActionResult> Question(Guid attemptId, int index)
    {
        var payload = await examService.GetQuestionAsync(attemptId, CurrentUserId, index);
        if (payload is null) return NotFound();

        var q = payload.Question;
        return Ok(new QuestionView(
            payload.Index,
            payload.Total,
            q.Id,
            q.Content,
            q.Type.ToString(),
            q.Points,
            q.Type == QuestionType.TextAnswer ? [] : q.Answers.Select(a => new ChoiceView(a.Id, a.Content)).ToList(),
            payload.CurrentAnswer?.AnswerIds,
            payload.CurrentAnswer?.TextInput));
    }

    [HttpPost("answer")]
    public async Task<IActionResult> Answer(SaveAnswerRequest request)
    {
        try
        {
            await examService.SaveAnswerAsync(request, CurrentUserId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
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

    [HttpPost("report")]
    public async Task<IActionResult> Report(ReportQuestionRequest request)
    {
        try
        {
            await examService.ReportQuestionAsync(request, CurrentUserId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
