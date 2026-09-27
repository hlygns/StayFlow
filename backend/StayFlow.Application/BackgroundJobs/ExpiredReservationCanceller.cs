using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Domain.Enums;

namespace StayFlow.Application.BackgroundJobs;

/// <summary>
/// Odeme suresi (PaymentDeadline, olusturulduktan 15 dk sonra) dolmus ve hala "odeme bekliyor"
/// durumundaki rezervasyonlari iptal eder; gecelerini tekrar satisa acar. Hangfire her dakika calistirir.
///
/// Neden her rezervasyon icin ayri bir "15 dk sonra calis" isi degil de dakikalik tarama?
/// Tek bir is kaybolsa (sunucu yeniden basladi, is silindi) rezervasyon sonsuza kadar odayi tutardi.
/// Tarama kendi kendini onarir: kacirilan her sey bir sonraki dakikada yakalanir.
/// </summary>
public class ExpiredReservationCanceller
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IReservationNotifier _notifier;
    private readonly ILogger<ExpiredReservationCanceller> _logger;

    public ExpiredReservationCanceller(
        IApplicationDbContext db, TimeProvider timeProvider, IReservationNotifier notifier, ILogger<ExpiredReservationCanceller> logger)
    {
        _db = db;
        _timeProvider = timeProvider;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Arka plan isinde HTTP istegi, dolayisiyla tenant yok: tum otellere bakiyoruz (IgnoreQueryFilters)
        var expired = await _db.Reservations.IgnoreQueryFilters()
            .Include(r => r.RoomNights)
            .Where(r => r.Status == ReservationStatus.Pending && r.PaymentDeadline != null && r.PaymentDeadline < now)
            .Take(500) // cok birikmisse her dakika bir parti
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            return 0;
        }

        foreach (var reservation in expired)
        {
            reservation.Status = ReservationStatus.Cancelled;
            reservation.PaymentDeadline = null;
            reservation.CancelledAt = now;
            _db.RoomNights.RemoveRange(reservation.RoomNights);
        }

        await _db.SaveChangesAsync(ct);

        foreach (var reservation in expired)
        {
            _logger.LogInformation("Odeme suresi doldu, rezervasyon otomatik iptal edildi {ReservationId} (otel {HotelId})",
                reservation.Id, reservation.HotelId);
            await _notifier.ReservationChangedAsync(reservation.HotelId, reservation.Id, ReservationChange.AutoCancelled, ct);
        }

        return expired.Count;
    }
}
