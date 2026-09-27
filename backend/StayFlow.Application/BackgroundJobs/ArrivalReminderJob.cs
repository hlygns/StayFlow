using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StayFlow.Application.Common;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Domain.Enums;

namespace StayFlow.Application.BackgroundJobs;

/// <summary>
/// Yarin giris yapacak, onaylanmis ve e-postasi olan misafirlere hatirlatma gonderir. Her sabah calisir.
/// ReminderSentAt sayesinde is iki kez calissa (yeniden deneme, elle tetikleme) misafir iki e-posta almaz.
/// </summary>
public class ArrivalReminderJob
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailSender _email;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ArrivalReminderJob> _logger;

    public ArrivalReminderJob(IApplicationDbContext db, IEmailSender email, TimeProvider timeProvider, ILogger<ArrivalReminderJob> logger)
    {
        _db = db;
        _email = email;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var tomorrow = _timeProvider.Today().AddDays(1);

        var arrivals = await _db.Reservations.IgnoreQueryFilters()
            .Include(r => r.Guest)
            .Include(r => r.Hotel)
            .Include(r => r.Room).ThenInclude(room => room.RoomType)
            .Where(r => r.CheckIn == tomorrow
                        && r.Status == ReservationStatus.Confirmed
                        && r.ReminderSentAt == null
                        && r.Guest.Email != null && r.Guest.Email != "")
            .ToListAsync(ct);

        var sent = 0;
        foreach (var reservation in arrivals)
        {
            var hotel = reservation.Hotel;
            var e = (string? value) => WebUtility.HtmlEncode(value ?? "");
            var body = $"""
                <p>Merhaba {e(reservation.Guest.FullName)},</p>
                <p><strong>{e(hotel.Name)}</strong> olarak sizi yarın ağırlamayı sabırsızlıkla bekliyoruz.</p>
                <ul>
                  <li>Giriş: {reservation.CheckIn:dd.MM.yyyy} (14:00'ten itibaren)</li>
                  <li>Çıkış: {reservation.CheckOut:dd.MM.yyyy} (12:00'ye kadar)</li>
                  <li>Oda: {e(reservation.Room.RoomType.Name)}</li>
                </ul>
                <p>Adres: {e(hotel.Address)}, {e(hotel.City)}<br>Telefon: {e(hotel.Phone)}</p>
                <p>İyi yolculuklar!</p>
                """;

            try
            {
                await _email.SendAsync(new EmailMessage(reservation.Guest.Email!, $"Yarın görüşmek üzere - {hotel.Name}", body), ct);
                reservation.ReminderSentAt = _timeProvider.GetUtcNow().UtcDateTime;
                await _db.SaveChangesAsync(ct); // her gonderimden sonra kaydet: yarida kesilirse gonderilenler tekrar gonderilmez
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Bir misafirin e-postasi basarisiz diye digerleri beklemesin; bir sonraki calismada tekrar denenir
                _logger.LogWarning(ex, "Hatirlatma e-postasi gonderilemedi {ReservationId}", reservation.Id);
            }
        }

        if (sent > 0)
        {
            _logger.LogInformation("{Count} misafire giris hatirlatmasi gonderildi", sent);
        }

        return sent;
    }
}
