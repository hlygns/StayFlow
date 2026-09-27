using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using StayFlow.Application.Common.Exceptions;
using StayFlow.Application.Common.Interfaces;

namespace StayFlow.Application.Reviews;

public record ReviewDto(
    Guid Id,
    int Rating,
    string Comment,
    string GuestName,
    string RoomTypeName,
    DateOnly StayCheckOut,
    DateTime CreatedAt,
    bool IsPublished,
    string? OwnerReply,
    DateTime? OwnerRepliedAt);

public record ReviewSummary(double? AverageRating, int Count, int[] Distribution, int Unanswered);

public class ReplyRequest
{
    [Required, MaxLength(2000)] public string Reply { get; set; } = string.Empty;
}

public class VisibilityRequest
{
    public bool IsPublished { get; set; }
}

/// <summary>Otel tarafi: yorumlari listele, yanitla, uygunsuz olani gizle.</summary>
public class ReviewService
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;

    public ReviewService(IApplicationDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<List<ReviewDto>> GetAllAsync(int? rating, bool? unanswered, CancellationToken ct = default) =>
        await _db.Reviews
            .Where(r => rating == null || r.Rating == rating)
            .Where(r => unanswered != true || r.OwnerReply == null)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewDto(r.Id, r.Rating, r.Comment, r.GuestName, r.Reservation.Room.RoomType.Name,
                r.Reservation.CheckOut, r.CreatedAt, r.IsPublished, r.OwnerReply, r.OwnerRepliedAt))
            .ToListAsync(ct);

    public async Task<ReviewSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        // Gizlenen yorumlar ortalamaya katilmaz (misafirin gordugu ortalamayla ayni olsun)
        var published = await _db.Reviews.Where(r => r.IsPublished).Select(r => new { r.Rating, r.OwnerReply }).ToListAsync(ct);
        var distribution = new int[5];
        foreach (var review in published) distribution[review.Rating - 1]++;

        return new ReviewSummary(
            published.Count == 0 ? null : Math.Round(published.Average(r => r.Rating), 1),
            published.Count,
            distribution,
            published.Count(r => r.OwnerReply == null));
    }

    public async Task<ReviewDto> ReplyAsync(Guid id, string reply, CancellationToken ct = default)
    {
        var review = await FindAsync(id, ct);
        review.OwnerReply = reply.Trim();
        review.OwnerRepliedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ReviewDto> SetVisibilityAsync(Guid id, bool isPublished, CancellationToken ct = default)
    {
        var review = await FindAsync(id, ct);
        review.IsPublished = isPublished;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<ReviewDto> GetAsync(Guid id, CancellationToken ct) =>
        (await GetAllAsync(null, null, ct)).Single(r => r.Id == id);

    private async Task<Domain.Entities.Review> FindAsync(Guid id, CancellationToken ct) =>
        await _db.Reviews.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Yorum bulunamadı.");
}

public static class GuestNames
{
    /// <summary>Herkese acik sayfada gizlilik: "Ayse Yilmaz" -> "Ayse Y."</summary>
    public static string Shorten(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "Misafir",
            1 => parts[0],
            _ => $"{parts[0]} {char.ToUpper(parts[^1][0], new System.Globalization.CultureInfo("tr-TR"))}."
        };
    }
}
