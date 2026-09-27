namespace StayFlow.Application.Common.Interfaces;

/// <summary>
/// Musaitligi etkileyen bir veri (oda, oda tipi, fiyat, rezervasyon, gece) kaydedilince DbContext bunu
/// cagirir. Gecersiz kilma tek bir yerde, SaveChanges'ta: yeni bir servis yazan gelistirici unutamaz.
/// </summary>
public interface IAvailabilityCacheInvalidator
{
    Task InvalidateAsync(IReadOnlyCollection<Guid> hotelIds, CancellationToken ct = default);
}
