using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using StayFlow.Application.Common.Exceptions;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Domain.Entities;

namespace StayFlow.Application.Rooms;

public record RoomDto(Guid Id, string RoomNumber, int Floor, bool IsActive, Guid RoomTypeId, string RoomTypeName);

public class SaveRoomRequest
{
    [Required, MaxLength(20)] public string RoomNumber { get; set; } = string.Empty;
    [Range(-5, 200)] public int Floor { get; set; }
    [Required] public Guid RoomTypeId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class RoomService
{
    private readonly IApplicationDbContext _db;

    public RoomService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<RoomDto>> GetAllAsync(Guid? roomTypeId, bool includeInactive, CancellationToken ct = default) =>
        await _db.Rooms
            .Where(r => roomTypeId == null || r.RoomTypeId == roomTypeId)
            .Where(r => includeInactive || r.IsActive)
            .OrderBy(r => r.RoomNumber)
            .Select(r => new RoomDto(r.Id, r.RoomNumber, r.Floor, r.IsActive, r.RoomTypeId, r.RoomType.Name))
            .ToListAsync(ct);

    public async Task<RoomDto> GetAsync(Guid id, CancellationToken ct = default) =>
        await _db.Rooms
            .Where(r => r.Id == id)
            .Select(r => new RoomDto(r.Id, r.RoomNumber, r.Floor, r.IsActive, r.RoomTypeId, r.RoomType.Name))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Oda bulunamadi.");

    public async Task<RoomDto> CreateAsync(SaveRoomRequest request, CancellationToken ct = default)
    {
        var room = new Room();
        await ApplyAsync(room, request, ct);

        _db.Rooms.Add(room);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(room.Id, ct);
    }

    public async Task<RoomDto> UpdateAsync(Guid id, SaveRoomRequest request, CancellationToken ct = default)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Oda bulunamadi.");

        if (room.IsActive && !request.IsActive)
        {
            await EnsureNoUpcomingReservationsAsync(id, ct);
        }

        await ApplyAsync(room, request, ct);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    /// <summary>
    /// Fiziksel silme yerine pasife alma (soft delete): gecmis rezervasyonlar bu odaya
    /// bagli, silersek raporlar ve gecmis kayitlar bozulur. Pasif oda musaitlikte cikmaz.
    /// </summary>
    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Oda bulunamadi.");

        await EnsureNoUpcomingReservationsAsync(id, ct);

        room.IsActive = false;
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureNoUpcomingReservationsAsync(Guid roomId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // RoomNight satiri = aktif (iptal edilmemis) rezervasyonun bir gecesi.
        if (await _db.RoomNights.AnyAsync(rn => rn.RoomId == roomId && rn.Date >= today, ct))
        {
            throw new ConflictException("Bu odanin ileri tarihli rezervasyonlari var; once onlari iptal edin veya tasiyin.");
        }
    }

    private async Task ApplyAsync(Room room, SaveRoomRequest request, CancellationToken ct)
    {
        // Oda tipi baska bir otele aitse tenant filtresi onu gizler -> "bulunamadi".
        // Boylece bir otel, baska bir otelin oda tipine oda baglayamaz.
        if (!await _db.RoomTypes.AnyAsync(rt => rt.Id == request.RoomTypeId, ct))
        {
            throw new NotFoundException("Oda tipi bulunamadi.");
        }

        var roomNumber = request.RoomNumber.Trim();
        if (await _db.Rooms.AnyAsync(r => r.RoomNumber == roomNumber && r.Id != room.Id, ct))
        {
            throw new ConflictException($"{roomNumber} numarali oda zaten var.");
        }

        room.RoomNumber = roomNumber;
        room.Floor = request.Floor;
        room.RoomTypeId = request.RoomTypeId;
        room.IsActive = request.IsActive;
    }
}
