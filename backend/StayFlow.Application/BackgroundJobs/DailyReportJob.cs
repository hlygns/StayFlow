using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StayFlow.Application.Common;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Domain.Enums;

namespace StayFlow.Application.BackgroundJobs;

public record DailyReport(
    string HotelName,
    DateOnly Date,
    int RoomCount,
    int OccupiedTonight,
    int ArrivalsToday,
    int DeparturesToday,
    int NewReservations,
    decimal NewReservationRevenue,
    int CancellationsToday)
{
    public double OccupancyRate => RoomCount == 0 ? 0 : (double)OccupiedTonight / RoomCount;
}

/// <summary>Her aksam otel sahiplerine gunun ozetini gonderir: doluluk, giris/cikis, yeni satislar, iptaller.</summary>
public class DailyReportJob
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private readonly IApplicationDbContext _db;
    private readonly IEmailSender _email;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DailyReportJob> _logger;

    public DailyReportJob(IApplicationDbContext db, IEmailSender email, TimeProvider timeProvider, ILogger<DailyReportJob> logger)
    {
        _db = db;
        _email = email;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var hotels = await _db.Hotels.Where(h => h.IsActive).Select(h => new { h.Id, h.Name }).ToListAsync(ct);

        foreach (var hotel in hotels)
        {
            var report = await BuildAsync(hotel.Id, hotel.Name, ct);
            var owners = await _db.Users.IgnoreQueryFilters()
                .Where(u => u.HotelId == hotel.Id && u.Role == UserRole.Owner)
                .Select(u => u.Email)
                .ToListAsync(ct);

            foreach (var owner in owners)
            {
                try
                {
                    await _email.SendAsync(new EmailMessage(owner, $"Gün sonu raporu - {report.Date:dd.MM.yyyy}", Render(report)), ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Gun sonu raporu gonderilemedi (otel {HotelId})", hotel.Id);
                }
            }
        }
    }

    public async Task<DailyReport> BuildAsync(Guid hotelId, string hotelName, CancellationToken ct = default)
    {
        var today = _timeProvider.Today();
        // Otelin bugununun UTC karsiligi: "bugun olusturulan rezervasyonlar" icin
        var zone = _timeProvider.LocalTimeZone;
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(today.ToDateTime(TimeOnly.MinValue), zone);
        var dayEndUtc = dayStartUtc.AddDays(1);

        var reservations = _db.Reservations.IgnoreQueryFilters().Where(r => r.HotelId == hotelId);

        var roomCount = await _db.Rooms.IgnoreQueryFilters().CountAsync(r => r.HotelId == hotelId && r.IsActive, ct);
        var occupied = await _db.RoomNights.IgnoreQueryFilters().CountAsync(rn => rn.HotelId == hotelId && rn.Date == today, ct);
        var arrivals = await reservations.CountAsync(r => r.CheckIn == today && r.Status != ReservationStatus.Cancelled, ct);
        var departures = await reservations.CountAsync(r => r.CheckOut == today && r.Status != ReservationStatus.Cancelled, ct);
        var created = await reservations
            .Where(r => r.CreatedAt >= dayStartUtc && r.CreatedAt < dayEndUtc && r.Status != ReservationStatus.Cancelled)
            .Select(r => r.TotalPrice)
            .ToListAsync(ct); // decimal toplami SQLite'ta cevrilemiyor; gunluk kayit sayisi kucuk, bellekte toplamak sorun degil
        var cancellations = await reservations.CountAsync(r =>
            r.Status == ReservationStatus.Cancelled && r.CancelledAt >= dayStartUtc && r.CancelledAt < dayEndUtc, ct);

        return new DailyReport(hotelName, today, roomCount, occupied, arrivals, departures, created.Count, created.Sum(), cancellations);
    }

    private static string Render(DailyReport r)
    {
        string Money(decimal value) => value.ToString("N0", Turkish) + " ₺";
        return $"""
            <h2 style="margin:0 0 4px">{WebUtility.HtmlEncode(r.HotelName)}</h2>
            <p style="color:#64748b;margin:0 0 16px">{r.Date.ToString("d MMMM yyyy dddd", Turkish)} · gün sonu raporu</p>
            <table cellpadding="6" style="border-collapse:collapse">
              <tr><td>Bu gece doluluk</td><td><strong>%{Math.Round(r.OccupancyRate * 100)}</strong> ({r.OccupiedTonight}/{r.RoomCount} oda)</td></tr>
              <tr><td>Bugün giriş</td><td><strong>{r.ArrivalsToday}</strong></td></tr>
              <tr><td>Bugün çıkış</td><td><strong>{r.DeparturesToday}</strong></td></tr>
              <tr><td>Bugün alınan rezervasyon</td><td><strong>{r.NewReservations}</strong> · {Money(r.NewReservationRevenue)}</td></tr>
              <tr><td>Bugün iptal edilen</td><td><strong>{r.CancellationsToday}</strong></td></tr>
            </table>
            """;
    }
}
