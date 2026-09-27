using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StayFlow.Application.Common.Interfaces;

namespace StayFlow.Application.Reservations;

/// <summary>
/// Musaitlik sonuclari icin onbellek (canlida Redis, testte/yerelde bellek).
///
/// Gecersiz kilma "surum anahtari" ile: her otelin bir surumu var ve anahtarlar surumu icerir:
///   availability:{otel}:{surum}:{giris}:{cikis}:{kisi}
/// Otelde oda/fiyat/rezervasyon degisince surum degisir; eski anahtarlar bir daha okunmaz, TTL ile silinir.
/// Tek tek anahtar aramaya (Redis'te KEYS/SCAN) gerek yok.
///
/// Yaris durumu da guvenli: sonuc hesaplanirken rezervasyon yapilirsa, sonuc ESKI surumun anahtarina yazilir
/// ve kimse onu okumaz. Ustelik onbellek sadece aramayi hizlandirir; cifte satis korumasi veritabaninda.
///
/// Redis erisilemezse hata firlatmaz: onbelleksiz hesaplamaya devam eder (Redis coktu diye arama durmasin).
/// </summary>
public class AvailabilityCache : IAvailabilityCacheInvalidator
{
    private static readonly TimeSpan ResultTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VersionTtl = TimeSpan.FromDays(1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly ILogger<AvailabilityCache> _logger;

    public AvailabilityCache(IDistributedCache cache, ILogger<AvailabilityCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T> GetOrCreateAsync<T>(Guid hotelId, string key, Func<Task<T>> factory, CancellationToken ct = default)
    {
        var version = await TryGetAsync(VersionKey(hotelId), ct);
        if (version is null)
        {
            version = NewVersion();
            await TrySetAsync(VersionKey(hotelId), version, VersionTtl, ct);
        }

        var fullKey = $"availability:{hotelId:N}:{version}:{key}";
        var cached = await TryGetAsync(fullKey, ct);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<T>(cached, Json)!;
        }

        var value = await factory();
        await TrySetAsync(fullKey, JsonSerializer.Serialize(value, Json), ResultTtl, ct);
        return value;
    }

    public async Task InvalidateAsync(IReadOnlyCollection<Guid> hotelIds, CancellationToken ct = default)
    {
        foreach (var hotelId in hotelIds)
        {
            await TrySetAsync(VersionKey(hotelId), NewVersion(), VersionTtl, ct);
        }
    }

    private static string VersionKey(Guid hotelId) => $"availability-version:{hotelId:N}";

    private static string NewVersion() => Guid.NewGuid().ToString("N")[..12];

    private async Task<string?> TryGetAsync(string key, CancellationToken ct)
    {
        try
        {
            return await _cache.GetStringAsync(key, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Onbellek okunamadi, veritabanindan hesaplaniyor");
            return null;
        }
    }

    private async Task TrySetAsync(string key, string value, TimeSpan ttl, CancellationToken ct)
    {
        try
        {
            await _cache.SetStringAsync(key, value, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Onbellege yazilamadi");
        }
    }
}
