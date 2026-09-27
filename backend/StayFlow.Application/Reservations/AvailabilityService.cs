using Microsoft.EntityFrameworkCore;
using StayFlow.Application.Common;
using StayFlow.Application.Common.Interfaces;

namespace StayFlow.Application.Reservations;

public record AvailableRoomDto(Guid Id, string RoomNumber, int Floor);

public record RoomTypeAvailabilityDto(
    Guid RoomTypeId,
    string RoomTypeName,
    int Capacity,
    int Nights,
    decimal TotalPrice,
    int AvailableRooms,
    List<AvailableRoomDto> Rooms);

public class AvailabilityService
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly AvailabilityCache? _cache;
    private readonly ICurrentTenantService? _tenant;

    /// <param name="cache">Verilmezse (unit testler) her sorgu dogrudan veritabanina gider.</param>
    public AvailabilityService(
        IApplicationDbContext db, TimeProvider timeProvider, AvailabilityCache? cache = null, ICurrentTenantService? tenant = null)
    {
        _db = db;
        _timeProvider = timeProvider;
        _cache = cache;
        _tenant = tenant;
    }

    /// <summary>
    /// Verilen tarihlerde bos odasi olan oda tiplerini, toplam konaklama fiyatiyla dondurur.
    /// "Bos oda" = aktif ve [checkIn, checkOut) araligindaki hicbir gecesi RoomNights'ta olmayan oda.
    /// </summary>
    public async Task<List<RoomTypeAvailabilityDto>> SearchAsync(
        DateOnly checkIn, DateOnly checkOut, int? guests, CancellationToken ct = default)
    {
        StayDates.Validate(checkIn, checkOut, _timeProvider.Today());

        if (_cache is not null && _tenant?.HotelId is { } hotelId)
        {
            return await _cache.GetOrCreateAsync(hotelId, $"{checkIn:yyyyMMdd}:{checkOut:yyyyMMdd}:{guests}",
                () => QueryAsync(checkIn, checkOut, guests, ct), ct);
        }

        return await QueryAsync(checkIn, checkOut, guests, ct);
    }

    private async Task<List<RoomTypeAvailabilityDto>> QueryAsync(DateOnly checkIn, DateOnly checkOut, int? guests, CancellationToken ct)
    {
        // Tarih cakisma mantigi tek satira indi: RoomNight tablosu sayesinde "iki aralik
        // cakisiyor mu?" yerine "bu araliktaki gecelerden biri dolu mu?" diye soruyoruz.
        var bookedRoomIds = _db.RoomNights
            .Where(rn => rn.Date >= checkIn && rn.Date < checkOut)
            .Select(rn => rn.RoomId);

        var roomTypes = await _db.RoomTypes
            .Where(rt => guests == null || rt.Capacity >= guests)
            .Select(rt => new
            {
                rt.Id,
                rt.Name,
                rt.Capacity,
                rt.BasePrice,
                Rooms = rt.Rooms
                    .Where(r => r.IsActive && !bookedRoomIds.Contains(r.Id))
                    .OrderBy(r => r.RoomNumber)
                    .Select(r => new AvailableRoomDto(r.Id, r.RoomNumber, r.Floor))
                    .ToList(),
                SeasonalPrices = rt.SeasonalPrices
                    .Where(sp => sp.StartDate < checkOut && sp.EndDate >= checkIn)
                    .ToList()
            })
            .ToListAsync(ct);

        var nights = checkOut.DayNumber - checkIn.DayNumber;

        return roomTypes
            .Where(rt => rt.Rooms.Count > 0)
            .Select(rt => new RoomTypeAvailabilityDto(
                rt.Id,
                rt.Name,
                rt.Capacity,
                nights,
                PriceCalculator.CalculateTotal(rt.BasePrice, rt.SeasonalPrices, checkIn, checkOut),
                rt.Rooms.Count,
                rt.Rooms))
            .OrderBy(rt => rt.TotalPrice)
            .ToList();
    }
}
