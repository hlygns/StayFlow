using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using StayFlow.Application.Common.Exceptions;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Domain.Entities;

namespace StayFlow.Application.Rooms;

public record RoomTypeDto(Guid Id, string Name, string Description, int Capacity, decimal BasePrice, int RoomCount);

public class SaveRoomTypeRequest
{
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(1000)] public string Description { get; set; } = string.Empty;
    [Range(1, 20)] public int Capacity { get; set; }
    [Range(typeof(decimal), "0.01", "1000000", ParseLimitsInInvariantCulture = true)] public decimal BasePrice { get; set; }
}

public class RoomTypeService
{
    private readonly IApplicationDbContext _db;

    public RoomTypeService(IApplicationDbContext db)
    {
        _db = db;
    }

    // Not: tenant filtresi sayesinde hicbir sorguda "Where(HotelId == ...)" yazmiyoruz.
    public async Task<List<RoomTypeDto>> GetAllAsync(CancellationToken ct = default) =>
        await _db.RoomTypes
            .OrderBy(rt => rt.Name)
            .Select(rt => new RoomTypeDto(rt.Id, rt.Name, rt.Description, rt.Capacity, rt.BasePrice,
                rt.Rooms.Count(r => r.IsActive)))
            .ToListAsync(ct);

    public async Task<RoomTypeDto> GetAsync(Guid id, CancellationToken ct = default) =>
        await _db.RoomTypes
            .Where(rt => rt.Id == id)
            .Select(rt => new RoomTypeDto(rt.Id, rt.Name, rt.Description, rt.Capacity, rt.BasePrice,
                rt.Rooms.Count(r => r.IsActive)))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Oda tipi bulunamadi.");

    public async Task<RoomTypeDto> CreateAsync(SaveRoomTypeRequest request, CancellationToken ct = default)
    {
        var roomType = new RoomType();
        Apply(roomType, request);

        _db.RoomTypes.Add(roomType);
        await _db.SaveChangesAsync(ct);

        return new RoomTypeDto(roomType.Id, roomType.Name, roomType.Description, roomType.Capacity, roomType.BasePrice, 0);
    }

    public async Task<RoomTypeDto> UpdateAsync(Guid id, SaveRoomTypeRequest request, CancellationToken ct = default)
    {
        var roomType = await FindAsync(id, ct);
        Apply(roomType, request);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var roomType = await FindAsync(id, ct);

        if (await _db.Rooms.AnyAsync(r => r.RoomTypeId == id, ct))
        {
            throw new ConflictException("Bu oda tipine bagli odalar var; once odalari baska tipe tasiyin.");
        }

        _db.SeasonalPrices.RemoveRange(_db.SeasonalPrices.Where(sp => sp.RoomTypeId == id));
        _db.RoomTypes.Remove(roomType);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<RoomType> FindAsync(Guid id, CancellationToken ct) =>
        await _db.RoomTypes.FirstOrDefaultAsync(rt => rt.Id == id, ct)
        ?? throw new NotFoundException("Oda tipi bulunamadi.");

    private static void Apply(RoomType roomType, SaveRoomTypeRequest request)
    {
        roomType.Name = request.Name.Trim();
        roomType.Description = request.Description.Trim();
        roomType.Capacity = request.Capacity;
        roomType.BasePrice = request.BasePrice;
    }
}
