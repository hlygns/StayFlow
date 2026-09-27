namespace StayFlow.Application.Common.Interfaces;

/// <summary>
/// "Su an istegi yapan kullanici hangi otele ait?" sorusunun cevabini soyutlar.
/// Gercek implementasyonu API katmaninda JWT claim'inden okuyarak yapilacak (Gun 3).
/// DbContext bu interface'e bagimli - boylece Infrastructure katmani ASP.NET Core'un
/// HttpContext'ini hic bilmek zorunda kalmiyor (katmanlar arasi bagimliligi temiz tutar).
/// </summary>
public interface ICurrentTenantService
{
    Guid? HotelId { get; }
}

/// <summary>
/// Oturum acilmamis (herkese acik) isteklerde tenant'i elle belirlemek icin: misafir uygulamasi
/// oteli slug ile secer, servis o otelin Id'sini buraya yazar ve istegin geri kalaninda tum sorgular
/// (query filter'lar, onbellek) o otelle sinirlanir. Istek (scope) bitince kaybolur.
/// </summary>
public interface ITenantOverride
{
    void UseHotel(Guid hotelId);
}
