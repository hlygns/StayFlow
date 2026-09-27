using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using StayFlow.Application.Common.Exceptions;
using StayFlow.Application.Common.Interfaces;

namespace StayFlow.Application.Hotels;

public record HotelDto(Guid Id, string Name, string Slug, string City, string Address, string Phone, string Email, string Description);

public class UpdateHotelRequest
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string City { get; set; } = string.Empty;
    [MaxLength(500)] public string Address { get; set; } = string.Empty;
    [MaxLength(30)] public string Phone { get; set; } = string.Empty;
    [EmailAddress] public string? Email { get; set; }
    [MaxLength(2000)] public string? Description { get; set; }
}

/// <summary>
/// Giris yapmis kullanicinin kendi oteli. Hotel tablosunda query filter yok (tenant'in kendisi),
/// bu yuzden burada Id'yi elle ICurrentTenantService'ten aliyoruz.
/// </summary>
public class HotelService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public HotelService(IApplicationDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<HotelDto> GetCurrentAsync(CancellationToken ct = default)
    {
        var hotel = await FindCurrentAsync(ct);
        return new HotelDto(hotel.Id, hotel.Name, hotel.Slug, hotel.City, hotel.Address, hotel.Phone, hotel.Email, hotel.Description);
    }

    public async Task<HotelDto> UpdateCurrentAsync(UpdateHotelRequest request, CancellationToken ct = default)
    {
        var hotel = await FindCurrentAsync(ct);

        // Slug bilerek degismiyor: disarida paylasilmis linkler kirilmasin.
        hotel.Name = request.Name.Trim();
        hotel.City = request.City.Trim();
        hotel.Address = request.Address.Trim();
        hotel.Phone = request.Phone.Trim();
        hotel.Email = request.Email?.Trim() ?? string.Empty;
        hotel.Description = request.Description?.Trim() ?? string.Empty;
        await _db.SaveChangesAsync(ct);

        return new HotelDto(hotel.Id, hotel.Name, hotel.Slug, hotel.City, hotel.Address, hotel.Phone, hotel.Email, hotel.Description);
    }

    private async Task<Domain.Entities.Hotel> FindCurrentAsync(CancellationToken ct)
    {
        var hotelId = _tenant.HotelId ?? throw new UnauthorizedException("Otel bilgisi bulunamadi.");
        return await _db.Hotels.FirstOrDefaultAsync(h => h.Id == hotelId, ct)
            ?? throw new NotFoundException("Otel bulunamadi.");
    }
}
