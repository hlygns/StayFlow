namespace StayFlow.Domain.Common;

/// <summary>
/// Bir otele ait olan (tenant'a bagli) her entity bu interface'i uygular.
/// DbContext, bu interface'i gorunce o tabloya otomatik "sadece kendi HotelId'n" filtresi ekler.
/// </summary>
public interface ITenantEntity
{
    Guid HotelId { get; set; }
}
