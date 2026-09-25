using Microsoft.EntityFrameworkCore;
using StayFlow.Application.Common.Exceptions;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Domain.Entities;
using StayFlow.Domain.Enums;

namespace StayFlow.Application.Reservations;

public class ReservationService
{
    /// <summary>Odemesi bu sure icinde yapilmayan rezervasyon Faz 2'de Hangfire ile otomatik iptal edilecek.</summary>
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(15);

    private const string RoomNotAvailableMessage = "Secilen oda bu tarihlerde musait degil.";

    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;

    public ReservationService(IApplicationDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<List<ReservationDto>> GetAllAsync(
        DateOnly? from, DateOnly? to, ReservationStatus? status, CancellationToken ct = default)
    {
        // from/to verilirse o araliga DOKUNAN (cakisan) rezervasyonlar gelir - takvim gorunumu icin.
        var query = _db.Reservations
            .Where(r => from == null || r.CheckOut > from)
            .Where(r => to == null || r.CheckIn < to)
            .Where(r => status == null || r.Status == status);

        return await Project(query.OrderBy(r => r.CheckIn)).ToListAsync(ct);
    }

    public async Task<ReservationDto> GetAsync(Guid id, CancellationToken ct = default) =>
        await Project(_db.Reservations.Where(r => r.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Rezervasyon bulunamadi.");

    /// <summary>
    /// Rezervasyon olusturma. Cifte rezervasyona karsi iki katmanli koruma:
    ///
    /// 1) On kontrol: oda bu tarihlerde dolu mu? Kullaniciya hizli ve anlasilir bir hata verir,
    ///    ama TEK BASINA YETMEZ - iki istek ayni anda kontrolden gecip ikisi de kaydedebilir.
    ///
    /// 2) Asil garanti: her gece icin bir RoomNight satiri ekliyoruz ve (RoomId, Date) uzerinde
    ///    UNIQUE index var. Rezervasyon + tum geceleri tek SaveChanges = tek transaction'da yazilir.
    ///    Yaris durumunda ikinci istek veritabani tarafindan reddedilir ve hicbir satiri kalmaz
    ///    (ya hepsi ya hicbiri). Uygulama kodu ne kadar hizli/yavas olursa olsun cifte satis imkansiz.
    /// </summary>
    public async Task<ReservationDto> CreateAsync(CreateReservationRequest request, CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        StayDates.Validate(request.CheckIn, request.CheckOut, DateOnly.FromDateTime(now));

        var room = await _db.Rooms
            .Include(r => r.RoomType)
                .ThenInclude(rt => rt.SeasonalPrices)
            .FirstOrDefaultAsync(r => r.Id == request.RoomId, ct);

        if (room is null || !room.IsActive)
        {
            throw new NotFoundException("Oda bulunamadi.");
        }

        if (request.GuestCount > room.RoomType.Capacity)
        {
            throw new ValidationException($"Bu oda en fazla {room.RoomType.Capacity} kisiliktir.");
        }

        // --- Katman 1: on kontrol ---
        var isBooked = await _db.RoomNights.AnyAsync(rn =>
            rn.RoomId == room.Id && rn.Date >= request.CheckIn && rn.Date < request.CheckOut, ct);
        if (isBooked)
        {
            throw new ConflictException(RoomNotAvailableMessage);
        }

        var guest = new Guest
        {
            FullName = request.Guest.FullName.Trim(),
            Email = request.Guest.Email?.Trim(),
            Phone = request.Guest.Phone?.Trim(),
            IdentityNumber = request.Guest.IdentityNumber?.Trim()
        };

        var reservation = new Reservation
        {
            RoomId = room.Id,
            Guest = guest,
            CheckIn = request.CheckIn,
            CheckOut = request.CheckOut,
            GuestCount = request.GuestCount,
            Status = ReservationStatus.Pending,
            TotalPrice = PriceCalculator.CalculateTotal(
                room.RoomType.BasePrice, room.RoomType.SeasonalPrices, request.CheckIn, request.CheckOut),
            PaymentDeadline = now.Add(PaymentWindow),
            CreatedAt = now
        };

        foreach (var night in PriceCalculator.Nights(request.CheckIn, request.CheckOut))
        {
            reservation.RoomNights.Add(new RoomNight { RoomId = room.Id, Date = night });
        }

        _db.Reservations.Add(reservation);

        // --- Katman 2: veritabani kisiti ---
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Bu noktada eklenen tek kisitli satirlar RoomNight'lar; on kontrolden sonra
            // baska bir istek ayni geceyi kapmis demektir. Transaction geri alindi, hicbir sey kaydedilmedi.
            throw new ConflictException(RoomNotAvailableMessage);
        }

        return await GetAsync(reservation.Id, ct);
    }

    public async Task<ReservationDto> ConfirmAsync(Guid id, CancellationToken ct = default)
    {
        var reservation = await FindAsync(id, ct);

        if (reservation.Status != ReservationStatus.Pending)
        {
            throw new ConflictException("Sadece bekleyen rezervasyonlar onaylanabilir.");
        }

        reservation.Status = ReservationStatus.Confirmed;
        reservation.PaymentDeadline = null;
        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    /// <summary>
    /// Iptal: rezervasyon kaydi (gecmis/rapor icin) kalir, ama RoomNight satirlari silinir -
    /// boylece o geceler tekrar satilabilir hale gelir.
    /// </summary>
    public async Task<ReservationDto> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var reservation = await _db.Reservations
            .Include(r => r.RoomNights)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Rezervasyon bulunamadi.");

        if (reservation.Status is not (ReservationStatus.Pending or ReservationStatus.Confirmed))
        {
            throw new ConflictException("Bu rezervasyon iptal edilemez (giris yapilmis, tamamlanmis veya zaten iptal).");
        }

        reservation.Status = ReservationStatus.Cancelled;
        reservation.PaymentDeadline = null;
        _db.RoomNights.RemoveRange(reservation.RoomNights);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    private async Task<Reservation> FindAsync(Guid id, CancellationToken ct) =>
        await _db.Reservations.FirstOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new NotFoundException("Rezervasyon bulunamadi.");

    private static IQueryable<ReservationDto> Project(IQueryable<Reservation> query) =>
        query.Select(r => new ReservationDto(
            r.Id,
            r.RoomId,
            r.Room.RoomNumber,
            r.Room.RoomType.Name,
            r.CheckIn,
            r.CheckOut,
            r.CheckOut.DayNumber - r.CheckIn.DayNumber,
            r.GuestCount,
            r.Status,
            r.TotalPrice,
            r.PaymentDeadline,
            r.CreatedAt,
            new GuestDto(r.Guest.Id, r.Guest.FullName, r.Guest.Email, r.Guest.Phone)));
}
