using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using StayFlow.Application.Common.Exceptions;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Domain.Entities;
using ValidationException = StayFlow.Application.Common.Exceptions.ValidationException;

namespace StayFlow.Application.Rooms;

public record SeasonalPriceDto(Guid Id, Guid RoomTypeId, DateOnly StartDate, DateOnly EndDate, decimal PricePerNight);

/// <summary>StartDate ve EndDate dahil: 1 Haziran - 31 Agustos = 31 Agustos gecesi de sezon fiyatindan.</summary>
public class SaveSeasonalPriceRequest
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    [Range(typeof(decimal), "0.01", "1000000", ParseLimitsInInvariantCulture = true)] public decimal PricePerNight { get; set; }
}

public class SeasonalPriceService
{
    private readonly IApplicationDbContext _db;

    public SeasonalPriceService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<SeasonalPriceDto>> GetAllAsync(Guid roomTypeId, CancellationToken ct = default)
    {
        await EnsureRoomTypeExistsAsync(roomTypeId, ct);

        return await _db.SeasonalPrices
            .Where(sp => sp.RoomTypeId == roomTypeId)
            .OrderBy(sp => sp.StartDate)
            .Select(sp => new SeasonalPriceDto(sp.Id, sp.RoomTypeId, sp.StartDate, sp.EndDate, sp.PricePerNight))
            .ToListAsync(ct);
    }

    public async Task<SeasonalPriceDto> CreateAsync(Guid roomTypeId, SaveSeasonalPriceRequest request, CancellationToken ct = default)
    {
        await EnsureRoomTypeExistsAsync(roomTypeId, ct);

        var price = new SeasonalPrice { RoomTypeId = roomTypeId };
        await ApplyAsync(price, request, ct);

        _db.SeasonalPrices.Add(price);
        await _db.SaveChangesAsync(ct);

        return ToDto(price);
    }

    public async Task<SeasonalPriceDto> UpdateAsync(Guid roomTypeId, Guid id, SaveSeasonalPriceRequest request, CancellationToken ct = default)
    {
        var price = await FindAsync(roomTypeId, id, ct);
        await ApplyAsync(price, request, ct);
        await _db.SaveChangesAsync(ct);

        return ToDto(price);
    }

    public async Task DeleteAsync(Guid roomTypeId, Guid id, CancellationToken ct = default)
    {
        var price = await FindAsync(roomTypeId, id, ct);
        _db.SeasonalPrices.Remove(price);
        await _db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(SeasonalPrice price, SaveSeasonalPriceRequest request, CancellationToken ct)
    {
        if (request.EndDate <= request.StartDate)
        {
            throw new ValidationException("Bitis tarihi baslangic tarihinden sonra olmali.");
        }

        // Ayni oda tipi icin iki sezon ust uste binerse "bu gece hangi fiyat?" belirsiz kalir.
        // Iki aralik [a1, a2] ve [b1, b2] ancak a1 <= b2 VE b1 <= a2 ise cakisir.
        var overlaps = await _db.SeasonalPrices.AnyAsync(sp =>
            sp.RoomTypeId == price.RoomTypeId
            && sp.Id != price.Id
            && sp.StartDate <= request.EndDate
            && request.StartDate <= sp.EndDate, ct);

        if (overlaps)
        {
            throw new ConflictException("Bu tarih araligi mevcut bir sezon fiyatiyla cakisiyor.");
        }

        price.StartDate = request.StartDate;
        price.EndDate = request.EndDate;
        price.PricePerNight = request.PricePerNight;
    }

    private async Task EnsureRoomTypeExistsAsync(Guid roomTypeId, CancellationToken ct)
    {
        if (!await _db.RoomTypes.AnyAsync(rt => rt.Id == roomTypeId, ct))
        {
            throw new NotFoundException("Oda tipi bulunamadi.");
        }
    }

    private async Task<SeasonalPrice> FindAsync(Guid roomTypeId, Guid id, CancellationToken ct) =>
        await _db.SeasonalPrices.FirstOrDefaultAsync(sp => sp.Id == id && sp.RoomTypeId == roomTypeId, ct)
        ?? throw new NotFoundException("Sezon fiyati bulunamadi.");

    private static SeasonalPriceDto ToDto(SeasonalPrice sp) =>
        new(sp.Id, sp.RoomTypeId, sp.StartDate, sp.EndDate, sp.PricePerNight);
}
