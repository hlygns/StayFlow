using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StayFlow.Application.Common;
using StayFlow.Application.Common.Exceptions;
using StayFlow.Application.Common.Helpers;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Application.Reservations;
using StayFlow.Application.Reviews;
using StayFlow.Domain.Entities;
using StayFlow.Domain.Enums;
using ValidationException = StayFlow.Application.Common.Exceptions.ValidationException;

namespace StayFlow.Application.Public;

/// <summary>
/// Misafir uygulamasinin (oturum acmadan) kullandigi islemler. Otel slug ile secilir ve istek boyunca
/// tenant o otele ayarlanir; boylece personel tarafinin tum kurallari (musaitlik, onbellek, cifte
/// rezervasyon korumasi, canli bildirim) aynen gecerli olur - is mantigi tekrar yazilmaz.
/// </summary>
public class PublicBookingService
{
    private const string DeclinedTestCard = "4000000000000002";

    private readonly IApplicationDbContext _db;
    private readonly ITenantOverride _tenant;
    private readonly AvailabilityService _availability;
    private readonly ReservationService _reservations;
    private readonly IEmailSender _email;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PublicBookingService> _logger;

    public PublicBookingService(
        IApplicationDbContext db,
        ITenantOverride tenant,
        AvailabilityService availability,
        ReservationService reservations,
        IEmailSender email,
        TimeProvider timeProvider,
        ILogger<PublicBookingService> logger)
    {
        _db = db;
        _tenant = tenant;
        _availability = availability;
        _reservations = reservations;
        _email = email;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // ---------- Otel arama ----------

    public async Task<List<PublicHotelSummary>> ListHotelsAsync(string? city, CancellationToken ct = default)
    {
        var query = _db.Hotels.Where(h => h.IsActive);
        if (!string.IsNullOrWhiteSpace(city))
        {
            var term = city.Trim();
            query = query.Where(h => h.City.Contains(term));
        }

        // Tenant'siz sorgu (tum oteller): filtreleri bilerek kapatiyoruz, sadece herkese acik alanlar okunuyor
        var hotels = await query
            .Select(h => new
            {
                h.Slug, h.Name, h.City, h.Description,
                Prices = _db.RoomTypes.IgnoreQueryFilters()
                    .Where(rt => rt.HotelId == h.Id && rt.Rooms.Any(r => r.IsActive))
                    .Select(rt => rt.BasePrice)
                    .ToList(),
                Ratings = _db.Reviews.IgnoreQueryFilters()
                    .Where(r => r.HotelId == h.Id && r.IsPublished)
                    .Select(r => r.Rating)
                    .ToList()
            })
            .OrderBy(h => h.Name)
            .ToListAsync(ct);

        return hotels
            .Where(h => h.Prices.Count > 0) // odasi olmayan (yeni kaydolmus) oteller listelenmez
            .Select(h => new PublicHotelSummary(
                h.Slug, h.Name, h.City, h.Description, h.Prices.Min(),
                h.Ratings.Count == 0 ? null : Math.Round(h.Ratings.Average(), 1), h.Ratings.Count))
            .ToList();
    }

    public async Task<PublicHotelDetail> GetHotelAsync(string slug, CancellationToken ct = default)
    {
        var hotel = await UseHotelAsync(slug, ct);
        var roomTypes = await _db.RoomTypes
            .Where(rt => rt.Rooms.Any(r => r.IsActive))
            .OrderBy(rt => rt.BasePrice)
            .Select(rt => new PublicRoomType(rt.Id, rt.Name, rt.Description, rt.Capacity, rt.BasePrice))
            .ToListAsync(ct);
        var ratings = await _db.Reviews.Where(r => r.IsPublished).Select(r => r.Rating).ToListAsync(ct);

        return new PublicHotelDetail(hotel.Slug, hotel.Name, hotel.City, hotel.Address, hotel.Phone, hotel.Description,
            ratings.Count == 0 ? null : Math.Round(ratings.Average(), 1), ratings.Count, roomTypes);
    }

    public async Task<List<PublicAvailability>> SearchAsync(
        string slug, DateOnly checkIn, DateOnly checkOut, int? guests, CancellationToken ct = default)
    {
        await UseHotelAsync(slug, ct);
        var results = await _availability.SearchAsync(checkIn, checkOut, guests, ct); // ayni sorgu + Redis onbellegi
        var descriptions = await _db.RoomTypes.ToDictionaryAsync(rt => rt.Id, rt => rt.Description, ct);

        return results
            .Select(r => new PublicAvailability(r.RoomTypeId, r.RoomTypeName, descriptions.GetValueOrDefault(r.RoomTypeId, ""),
                r.Capacity, r.Nights, r.TotalPrice, r.AvailableRooms))
            .ToList();
    }

    // ---------- Rezervasyon ----------

    public async Task<GuestReservation> BookAsync(string slug, PublicBookingRequest request, CancellationToken ct = default)
    {
        await UseHotelAsync(slug, ct);
        var created = await _reservations.CreateForRoomTypeAsync(
            request.RoomTypeId, request.CheckIn, request.CheckOut, request.GuestCount,
            new GuestInput { FullName = request.Guest.FullName, Email = request.Guest.Email.Trim(), Phone = request.Guest.Phone },
            ct);

        _logger.LogInformation("Misafir uygulamasindan rezervasyon {ConfirmationCode} ({Hotel})", created.ConfirmationCode, slug);
        return await GetAsync(created.ConfirmationCode!, request.Guest.Email, ct);
    }

    public async Task<GuestReservation> GetAsync(string code, string email, CancellationToken ct = default)
    {
        var reservation = await FindForGuestAsync(code, email, ct);
        return ToGuestReservation(reservation);
    }

    public async Task<GuestReservation> CancelAsync(string code, string email, CancellationToken ct = default)
    {
        var reservation = await FindForGuestAsync(code, email, ct);
        if (!CanCancel(reservation))
        {
            throw new ConflictException("Bu rezervasyon artık uygulamadan iptal edilemez. Lütfen otelle iletişime geçin.");
        }

        _tenant.UseHotel(reservation.HotelId);
        await _reservations.CancelAsync(reservation.Id, ct);

        // Odenmisse (simule) iade edildi olarak isaretle
        foreach (var payment in reservation.Payments.Where(p => p.Status == PaymentStatus.Paid))
        {
            payment.Status = PaymentStatus.Refunded;
        }
        await _db.SaveChangesAsync(ct);

        return await GetAsync(code, email, ct);
    }

    public async Task<GuestReservation> PayAsync(string code, SimulatedPaymentRequest request, CancellationToken ct = default)
    {
        var reservation = await FindForGuestAsync(code, request.Email, ct);
        if (!CanPay(reservation))
        {
            throw new ConflictException(reservation.Status == ReservationStatus.Cancelled
                ? "Ödeme süresi dolduğu için rezervasyon iptal edildi. Lütfen yeniden rezervasyon yapın."
                : "Bu rezervasyon için ödeme beklenmiyor.");
        }

        var cardNumber = new string(request.CardNumber.Where(char.IsDigit).ToArray());
        if (cardNumber.Length is < 15 or > 16 || !PassesLuhn(cardNumber))
        {
            throw new ValidationException("Kart numarası geçersiz.");
        }
        if (cardNumber == DeclinedTestCard)
        {
            throw new ValidationException("Kart bankanız tarafından reddedildi. Lütfen başka bir kart deneyin.");
        }

        _tenant.UseHotel(reservation.HotelId);
        _db.Payments.Add(new Payment
        {
            HotelId = reservation.HotelId,
            ReservationId = reservation.Id,
            Amount = reservation.TotalPrice,
            Status = PaymentStatus.Paid,
            Method = $"Kart •••• {cardNumber[^4..]} (simülasyon)",
            PaidAt = _timeProvider.GetUtcNow().UtcDateTime
        });
        await _db.SaveChangesAsync(ct);
        await _reservations.ConfirmAsync(reservation.Id, ct); // onay + panele canli bildirim

        await SendConfirmationAsync(reservation, ct);
        return await GetAsync(code, request.Email, ct);
    }

    // ---------- Yorumlar ----------

    /// <summary>Sadece konaklamasi bitmis (check-out) misafir, rezervasyon basina bir kez yorum yazabilir.</summary>
    public async Task<GuestReservation> AddReviewAsync(string code, GuestReviewRequest request, CancellationToken ct = default)
    {
        var reservation = await FindForGuestAsync(code, request.Email, ct);
        if (reservation.Status != ReservationStatus.CheckedOut)
        {
            throw new ConflictException("Yorum, konaklamanız tamamlandıktan sonra yazılabilir.");
        }
        if (reservation.Review is not null)
        {
            throw new ConflictException("Bu konaklama için zaten yorum yazdınız.");
        }

        _db.Reviews.Add(new Review
        {
            HotelId = reservation.HotelId,
            ReservationId = reservation.Id,
            Rating = request.Rating,
            Comment = request.Comment.Trim(),
            GuestName = reservation.Guest.FullName,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        });
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Yeni yorum ({Rating} yildiz) {ConfirmationCode}", request.Rating, reservation.ConfirmationCode);

        return await GetAsync(code, request.Email, ct);
    }

    public async Task<List<PublicReview>> GetReviewsAsync(string slug, CancellationToken ct = default)
    {
        await UseHotelAsync(slug, ct);
        var reviews = await _db.Reviews
            .Where(r => r.IsPublished)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new { r.Rating, r.Comment, r.GuestName, r.CreatedAt, r.OwnerReply })
            .ToListAsync(ct);

        return reviews.Select(r => new PublicReview(r.Rating, r.Comment, GuestNames.Shorten(r.GuestName), r.CreatedAt, r.OwnerReply)).ToList();
    }

    // ---------- Yardimcilar ----------

    private async Task<Hotel> UseHotelAsync(string slug, CancellationToken ct)
    {
        var hotel = await _db.Hotels.FirstOrDefaultAsync(h => h.Slug == slug && h.IsActive, ct)
            ?? throw new NotFoundException("Otel bulunamadı.");
        _tenant.UseHotel(hotel.Id);
        return hotel;
    }

    /// <summary>
    /// Kod VE e-posta eslesmeli. Ikisinden biri yanlissa ayni cevap: "bulunamadi" - hangisinin yanlis
    /// oldugu soylenmez (kod tahmini ile baskasinin rezervasyonunu bulmayi zorlastirir).
    /// </summary>
    private async Task<Reservation> FindForGuestAsync(string code, string email, CancellationToken ct)
    {
        var normalizedCode = ConfirmationCode.Normalize(code);
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var reservation = await _db.Reservations.IgnoreQueryFilters()
            .Include(r => r.Hotel)
            .Include(r => r.Guest)
            .Include(r => r.Room).ThenInclude(room => room.RoomType)
            .Include(r => r.Payments)
            .Include(r => r.Review)
            .FirstOrDefaultAsync(r => r.ConfirmationCode == normalizedCode, ct);

        if (reservation is null || reservation.Guest.Email?.Trim().ToLowerInvariant() != normalizedEmail)
        {
            throw new NotFoundException("Rezervasyon bulunamadı. Kodu ve e-posta adresini kontrol edin.");
        }

        return reservation;
    }

    private bool CanPay(Reservation r) =>
        r.Status == ReservationStatus.Pending && r.PaymentDeadline > _timeProvider.GetUtcNow().UtcDateTime;

    private bool CanCancel(Reservation r) =>
        (r.Status is ReservationStatus.Pending or ReservationStatus.Confirmed) && r.CheckIn > _timeProvider.Today();

    private GuestReservation ToGuestReservation(Reservation r) => new(
        r.ConfirmationCode!,
        r.Status,
        r.Hotel.Slug,
        r.Hotel.Name,
        $"{r.Hotel.Address}, {r.Hotel.City}".Trim(' ', ','),
        r.Hotel.Phone,
        r.Room.RoomType.Name,
        r.CheckIn,
        r.CheckOut,
        r.CheckOut.DayNumber - r.CheckIn.DayNumber,
        r.GuestCount,
        r.TotalPrice,
        r.Status == ReservationStatus.Pending ? r.PaymentDeadline : null,
        r.Guest.FullName,
        CanPay(r),
        CanCancel(r),
        r.Payments.OrderByDescending(p => p.PaidAt).FirstOrDefault()?.Method,
        r.Status == ReservationStatus.CheckedOut && r.Review is null,
        r.Review?.Rating);

    private static bool PassesLuhn(string digits)
    {
        var sum = 0;
        var doubleIt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubleIt && (d *= 2) > 9) d -= 9;
            sum += d;
            doubleIt = !doubleIt;
        }
        return sum % 10 == 0;
    }

    private async Task SendConfirmationAsync(Reservation r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Guest.Email)) return;
        var e = (string? v) => WebUtility.HtmlEncode(v ?? "");
        try
        {
            await _email.SendAsync(new EmailMessage(r.Guest.Email, $"Rezervasyonunuz onaylandı - {r.Hotel.Name}", $"""
                <p>Merhaba {e(r.Guest.FullName)},</p>
                <p><strong>{e(r.Hotel.Name)}</strong> rezervasyonunuz onaylandı.</p>
                <p style="font-size:20px;letter-spacing:2px"><strong>{r.ConfirmationCode}</strong></p>
                <ul>
                  <li>{r.CheckIn:dd.MM.yyyy} – {r.CheckOut:dd.MM.yyyy} ({r.CheckOut.DayNumber - r.CheckIn.DayNumber} gece)</li>
                  <li>{e(r.Room.RoomType.Name)}, {r.GuestCount} kişi</li>
                  <li>Toplam: {r.TotalPrice:N0} TL</li>
                </ul>
                <p>Rezervasyonunuzu StayFlow uygulamasında bu kod ve e-posta adresinizle görebilirsiniz.</p>
                """), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Onay e-postasi gitmedi diye odeme geri alinmaz; misafir kodu uygulamada zaten goruyor
            _logger.LogWarning(ex, "Onay e-postasi gonderilemedi {ConfirmationCode}", r.ConfirmationCode);
        }
    }
}
