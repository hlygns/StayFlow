using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Application.Reviews;

namespace StayFlow.API.Controllers;

/// <summary>Yorum yonetimi: tum personel okur ve yanitlar, gizleme sadece otel sahibi.</summary>
[ApiController]
[Authorize(Roles = Roles.Staff)]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly ReviewService _reviews;

    public ReviewsController(ReviewService reviews)
    {
        _reviews = reviews;
    }

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
