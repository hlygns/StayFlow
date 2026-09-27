using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StayFlow.Application.Common;
using StayFlow.Application.Common.Exceptions;
using StayFlow.Application.Common.Helpers;
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
    private readonly ILogger<ReservationService> _logger;
    private readonly IReservationNotifier _notifier;

    public ReservationService(
        IApplicationDbContext db,
        TimeProvider timeProvider,
        ILogger<ReservationService>? logger = null,
        IReservationNotifier? notifier = null)
    {
        _db = db;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<ReservationService>.Instance;
        _notifier = notifier ?? NullReservationNotifier.Instance;
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
        StayDates.Validate(request.CheckIn, request.CheckOut, _timeProvider.Today());

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
            ConfirmationCode = ConfirmationCode.New(),
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
            // Basarisiz eklemeler (rezervasyon, misafir, geceler) hala "eklenecek" olarak izleniyor;
            // temizlemezsek ayni istekteki sonraki SaveChanges (ör. baska oda denemesi) onlari tekrar ekler.
            _db.ChangeTracker.Clear();
            // Bu noktada eklenen tek kisitli satirlar RoomNight'lar; on kontrolden sonra
            // baska bir istek ayni geceyi kapmis demektir. Transaction geri alindi, hicbir sey kaydedilmedi.
            // Uyari olarak loglaniyor: canlida bu korumanin kac kez devreye girdigi izlenebilsin.
            _logger.LogWarning(
                "Cifte rezervasyon veritabani kisitiyla engellendi: oda {RoomId}, {CheckIn} - {CheckOut}",
                room.Id, request.CheckIn, request.CheckOut);
            throw new ConflictException(RoomNotAvailableMessage);
        }

        _logger.LogInformation(
            "Rezervasyon olusturuldu {ReservationId}: oda {RoomNumber}, {CheckIn} - {CheckOut}, {TotalPrice} TL",
            reservation.Id, room.RoomNumber, reservation.CheckIn, reservation.CheckOut, reservation.TotalPrice);
        await _notifier.ReservationChangedAsync(room.HotelId, reservation.Id, ReservationChange.Created, ct);

        return await GetAsync(reservation.Id, ct);
    }

    /// <summary>
    /// Misafir belirli bir odayi degil oda tipini secer (mobil uygulama). Bu tipteki bos odalar sirayla
    /// denenir: iki misafir ayni anda son odalara saldirirsa kaybeden, varsa bir sonraki bos odayi alir.
    /// Hic oda kalmadiysa 409.
    /// </summary>
    public async Task<ReservationDto> CreateForRoomTypeAsync(
        Guid roomTypeId, DateOnly checkIn, DateOnly checkOut, int guestCount, GuestInput guest, CancellationToken ct = default)
    {
        StayDates.Validate(checkIn, checkOut, _timeProvider.Today());

        var candidateRoomIds = await _db.Rooms
            .Where(r => r.RoomTypeId == roomTypeId && r.IsActive)
            .Where(r => !_db.RoomNights.Any(rn => rn.RoomId == r.Id && rn.Date >= checkIn && rn.Date < checkOut))
            .OrderBy(r => r.RoomNumber)
            .Select(r => r.Id)
            .Take(5) // en fazla 5 deneme: yogun anda sonsuz donguye girmesin
            .ToListAsync(ct);

        foreach (var roomId in candidateRoomIds)
        {
            try
            {
                return await CreateAsync(new CreateReservationRequest
                {
                    RoomId = roomId, CheckIn = checkIn, CheckOut = checkOut, GuestCount = guestCount, Guest = guest
                }, ct);
            }
            catch (ConflictException)
            {
                // Bu oda az once baskasina satildi: siradaki bos odayi dene
            }
        }

        throw new ConflictException("Seçtiğiniz oda tipinde bu tarihler için müsait oda kalmadı.");
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
        await _notifier.ReservationChangedAsync(reservation.HotelId, reservation.Id, ReservationChange.Confirmed, ct);

        return await GetAsync(id, ct);
    }

    /// <summary>
    /// Misafir otele geldi. Sadece onaylanmis rezervasyon, giris gunu ya da sonrasinda (ama cikistan once)
    /// check-in yapabilir - yanlis rezervasyona ya da gunler oncesinden giris yapilmasini engeller.
    /// </summary>
    public async Task<ReservationDto> CheckInAsync(Guid id, CancellationToken ct = default)
    {
        var reservation = await FindAsync(id, ct);
        var today = Today();

        if (reservation.Status != ReservationStatus.Confirmed)
        {
            throw new ConflictException("Sadece onaylanmis rezervasyonlara check-in yapilabilir.");
        }

        if (today < reservation.CheckIn || today >= reservation.CheckOut)
        {
            throw new ConflictException("Check-in sadece konaklama tarihleri icinde yapilabilir.");
        }

        reservation.Status = ReservationStatus.CheckedIn;
        await _db.SaveChangesAsync(ct);
        await _notifier.ReservationChangedAsync(reservation.HotelId, reservation.Id, ReservationChange.CheckedIn, ct);

        return await GetAsync(id, ct);
    }

    /// <summary>
    /// Misafir ayrildi. Planlanandan erken ayrilirsa kalan gecelerin RoomNight satirlari silinir:
    /// oda o gecelerde tekrar satilabilir. (Ucret iadesi odeme modulunun isi - fiyat degismiyor.)
    /// </summary>
    public async Task<ReservationDto> CheckOutAsync(Guid id, CancellationToken ct = default)
    {
        var reservation = await _db.Reservations
            .Include(r => r.RoomNights)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Rezervasyon bulunamadi.");

        if (reservation.Status != ReservationStatus.CheckedIn)
        {
            throw new ConflictException("Sadece konaklayan misafirin check-out'u yapilabilir.");
        }

        // Giris gunu ayrilsa bile ilk gece kullanilmis sayilir; bosaltilan geceler en erken ertesi gundur.
        var firstFreedNight = Max(Today(), reservation.CheckIn.AddDays(1));
        if (firstFreedNight < reservation.CheckOut)
        {
            _db.RoomNights.RemoveRange(reservation.RoomNights.Where(rn => rn.Date >= firstFreedNight));
            reservation.CheckOut = firstFreedNight;
        }

        reservation.Status = ReservationStatus.CheckedOut;
        await _db.SaveChangesAsync(ct);
        await _notifier.ReservationChangedAsync(reservation.HotelId, reservation.Id, ReservationChange.CheckedOut, ct);

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
        reservation.CancelledAt = _timeProvider.GetUtcNow().UtcDateTime;
        _db.RoomNights.RemoveRange(reservation.RoomNights);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Rezervasyon iptal edildi {ReservationId}", reservation.Id);
        await _notifier.ReservationChangedAsync(reservation.HotelId, reservation.Id, ReservationChange.Cancelled, ct);

        return await GetAsync(id, ct);
    }

    private DateOnly Today() => _timeProvider.Today();

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

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
            r.ConfirmationCode,
            new GuestDto(r.Guest.Id, r.Guest.FullName, r.Guest.Email, r.Guest.Phone)));
}
