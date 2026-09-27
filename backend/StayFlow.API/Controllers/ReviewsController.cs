using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StayFlow.API.Infrastructure;
using StayFlow.Application.Reviews;

namespace StayFlow.API.Controllers;

/// <summary>Yorum yonetimi: tum personel okur ve yanitlar, gizleme sadece otel sahibi.</summary>
[ApiController]
[Authorize(Roles = Roles.Staff)]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly ReviewService _reviews;
    private readonly ReviewInsightService _insights;

    public ReviewsController(ReviewService reviews, ReviewInsightService insights)
    {
        _reviews = reviews;
        _insights = insights;
    }

    /// <summary>Panel, yapay zeka butonlarini gostermeden once sorar.</summary>
    [HttpGet("ai-status")]
    public ActionResult<object> AiStatus() => Ok(new { available = _insights.IsAvailable });

    [EnableRateLimiting(RateLimitPolicies.Ai)]
    [HttpGet("insights")]
    public async Task<ActionResult<ReviewInsights>> Insights(CancellationToken ct) =>
        Ok(await _insights.GetInsightsAsync(ct));

    [EnableRateLimiting(RateLimitPolicies.Ai)]
    [HttpPost("{id:guid}/suggest-reply")]
    public async Task<ActionResult<object>> SuggestReply(Guid id, CancellationToken ct) =>
        Ok(new { reply = await _insights.SuggestReplyAsync(id, ct) });

    [HttpGet]
    public async Task<ActionResult<List<ReviewDto>>> GetAll([FromQuery] int? rating, [FromQuery] bool? unanswered, CancellationToken ct) =>
        Ok(await _reviews.GetAllAsync(rating, unanswered, ct));

    [HttpGet("summary")]
    public async Task<ActionResult<ReviewSummary>> Summary(CancellationToken ct) =>
        Ok(await _reviews.GetSummaryAsync(ct));

    [HttpPut("{id:guid}/reply")]
    public async Task<ActionResult<ReviewDto>> Reply(Guid id, ReplyRequest request, CancellationToken ct) =>
        Ok(await _reviews.ReplyAsync(id, request.Reply, ct));

    [Authorize(Roles = Roles.Owner)]
    [HttpPut("{id:guid}/visibility")]
    public async Task<ActionResult<ReviewDto>> Visibility(Guid id, VisibilityRequest request, CancellationToken ct) =>
        Ok(await _reviews.SetVisibilityAsync(id, request.IsPublished, ct));
}
