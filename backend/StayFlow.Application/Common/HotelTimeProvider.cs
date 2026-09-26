namespace StayFlow.Application.Common;

/// <summary>
/// Sistem saati + otellerin saat dilimi. Sunucu (ve container) UTC calisir, ama "bugun" otelin bugunudur:
/// Antalya'da gece 01:00'de gelen misafir icin UTC hala "dun"dur ve check-in reddedilirdi.
/// Servisler tarihi GetLocalNow() ile alir; testlerde FakeTimeProvider ile saat ve dilim ayarlanabilir.
/// </summary>
public sealed class HotelTimeProvider(TimeZoneInfo hotelTimeZone) : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone { get; } = hotelTimeZone;
}

public static class TimeProviderExtensions
{
    /// <summary>Otelin saat dilimine gore bugunun tarihi.</summary>
    public static DateOnly Today(this TimeProvider timeProvider) => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
}
