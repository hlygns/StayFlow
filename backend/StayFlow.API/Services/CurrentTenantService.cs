using StayFlow.Application.Common.Interfaces;

namespace StayFlow.API.Services;

/// <summary>
/// ICurrentTenantService'in gercek implementasyonu: giris yapmis kullanicinin JWT'sindeki
/// "hotelId" claim'ini okur. Claim henuz yok (Gun 3'te JWT uretimine eklenecek) - o zamana
/// kadar HotelId hep null doner, DbContext'teki fail-closed filtre sayesinde bu guvenli bir durum.
/// </summary>
public class CurrentTenantService : ICurrentTenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? HotelId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst("hotelId")?.Value;
            return Guid.TryParse(claim, out var hotelId) ? hotelId : null;
        }
    }
}
