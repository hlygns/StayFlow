using StayFlow.Application.Common.Interfaces;

namespace StayFlow.API.Services;

/// <summary>
/// ICurrentTenantService'in gercek implementasyonu: giris yapmis kullanicinin JWT'sindeki
/// "hotelId" claim'ini okur. Herkese acik uclarda (misafir uygulamasi) claim yoktur; orada servis
/// oteli slug'dan bulup UseHotel ile belirler. Hicbiri yoksa null: fail-closed filtre hicbir satir dondurmez.
/// </summary>
public class CurrentTenantService : ICurrentTenantService, ITenantOverride
{
    private Guid? _override;

    /// <summary>
    /// Sadece herkese acik uclarda (PublicBookingService) cagrilir. O uclar yalnizca herkese acik veriyi ve
    /// "kod + e-posta" ile korunan islemleri sunar; bu yuzden istekte bir personel token'i olsa bile
    /// secilen otel gecerlidir (panelde oturumu acik biri baska bir otelin sayfasina bakabilmeli).
    /// </summary>
    public void UseHotel(Guid hotelId) => _override = hotelId;

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? HotelId => _override ?? ClaimHotelId;

    private Guid? ClaimHotelId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst("hotelId")?.Value;
            return Guid.TryParse(claim, out var hotelId) ? hotelId : null;
        }
    }
}
