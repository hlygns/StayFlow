using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using StayFlow.Application.Common.Exceptions;
using StayFlow.Application.Common.Interfaces;

namespace StayFlow.Application.Reviews;

public record ReviewForAi(int Rating, string Comment, DateTime CreatedAt);

public record ReviewInsights(
    string Summary,
    List<string> Strengths,
    List<string> Complaints,
    List<string> Suggestions,
    int ReviewCount,
    DateTime GeneratedAt);

/// <summary>
/// Yorum analizi yapan yapay zeka. Application katmani saglayiciyi bilmez: Infrastructure'da Claude
/// uygulamasi var; baska bir saglayiciya gecmek icin yalnizca bu arayuzun yeni bir uygulamasi yazilir.
/// </summary>
public interface IReviewAssistant
{
    bool IsConfigured { get; }

    Task<ReviewInsights> SummarizeAsync(string hotelName, IReadOnlyList<ReviewForAi> reviews, CancellationToken ct = default);

    Task<string> SuggestReplyAsync(string hotelName, ReviewForAi review, CancellationToken ct = default);
}

/// <summary>Anahtar tanimli degilse: ozellik kapali, cagrilar anlasilir bir 503 ile doner.</summary>
public sealed class UnconfiguredReviewAssistant : IReviewAssistant
{
    public bool IsConfigured => false;

    public Task<ReviewInsights> SummarizeAsync(string hotelName, IReadOnlyList<ReviewForAi> reviews, CancellationToken ct = default) =>
        throw new FeatureUnavailableException("Yapay zekâ özelliği bu kurulumda yapılandırılmamış (ANTHROPIC_API_KEY).");

    public Task<string> SuggestReplyAsync(string hotelName, ReviewForAi review, CancellationToken ct = default) =>
        throw new FeatureUnavailableException("Yapay zekâ özelliği bu kurulumda yapılandırılmamış (ANTHROPIC_API_KEY).");
}

/// <summary>
/// Paneldeki "yapay zeka ozeti" ve "yanit oner". Ozet onbellege alinir: anahtar, otelin yayindaki
/// yorumlarinin (id + icerik) hash'i. Yeni yorum gelmedikce ayni ozet tekrar hesaplanmaz - her tiklama
/// ucretli bir API cagrisi olmasin.
/// </summary>
public class ReviewInsightService
{
    public const int MaxReviewsForSummary = 150;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(7);

    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _tenant;
    private readonly IReviewAssistant _assistant;
    private readonly IDistributedCache _cache;

    public ReviewInsightService(IApplicationDbContext db, ICurrentTenantService tenant, IReviewAssistant assistant, IDistributedCache cache)
    {
        _db = db;
        _tenant = tenant;
        _assistant = assistant;
        _cache = cache;
    }

    public bool IsAvailable => _assistant.IsConfigured;

    public async Task<ReviewInsights> GetInsightsAsync(CancellationToken ct = default)
    {
        // Ozellik kapaliysa once bunu soyle: "en az 3 yorum gerekli" gibi yaniltici bir mesaj vermesin
        if (!_assistant.IsConfigured)
        {
            throw new FeatureUnavailableException("Yapay zekâ özelliği bu kurulumda yapılandırılmamış (ANTHROPIC_API_KEY).");
        }

        var hotelId = _tenant.HotelId ?? throw new UnauthorizedException("Otel bilgisi bulunamadı.");
        var reviews = await _db.Reviews
            .Where(r => r.IsPublished)
            .OrderByDescending(r => r.CreatedAt)
            .Take(MaxReviewsForSummary) // en guncel yorumlar; cok eski yorumlar bugunku oteli yansitmaz
            .Select(r => new { r.Id, r.Rating, r.Comment, r.CreatedAt })
            .ToListAsync(ct);

        if (reviews.Count < 3)
        {
            throw new ValidationException("Özet için en az 3 yayında yorum gerekiyor.");
        }

        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('|', reviews.Select(r => $"{r.Id}:{r.Rating}:{r.Comment}")))))[..16];
        var cacheKey = $"review-insights:{hotelId:N}:{fingerprint}";

        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<ReviewInsights>(cached)!;
        }

        var hotelName = await _db.Hotels.Where(h => h.Id == hotelId).Select(h => h.Name).FirstAsync(ct);
        var insights = await _assistant.SummarizeAsync(
            hotelName, reviews.Select(r => new ReviewForAi(r.Rating, r.Comment, r.CreatedAt)).ToList(), ct);

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(insights),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl }, ct);
        return insights;
    }

    public async Task<string> SuggestReplyAsync(Guid reviewId, CancellationToken ct = default)
    {
        var review = await _db.Reviews
            .Where(r => r.Id == reviewId)
            .Select(r => new { r.Rating, r.Comment, r.CreatedAt, HotelName = r.Hotel.Name })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Yorum bulunamadı.");

        return await _assistant.SuggestReplyAsync(review.HotelName, new ReviewForAi(review.Rating, review.Comment, review.CreatedAt), ct);
    }
}
