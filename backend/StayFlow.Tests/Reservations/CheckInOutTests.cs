using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using StayFlow.Application.Common.Exceptions;
using StayFlow.Application.Reservations;
using StayFlow.Domain.Enums;
using StayFlow.Tests.Common;

namespace StayFlow.Tests.Reservations;

/// <summary>
/// Check-in/out "bugun"e bagli kurallar icerir. FakeTimeProvider ile saati elle ilerletiyoruz:
/// rezervasyonu 1 Temmuz'da yapip 10 Temmuz'a "zaman yolculugu" yapabiliyoruz.
/// </summary>
public class CheckInOutTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2030, 7, 1, 9, 0, 0, TimeSpan.Zero));

    private static readonly DateOnly July10 = new(2030, 7, 10);

    private ReservationService Service(SeededHotel hotel) => new(_database.CreateContext(hotel.Tenant), _clock);

    private void SetToday(DateOnly date) => _clock.SetUtcNow(new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));

    /// <summary>10-14 Temmuz (4 gece), onaylanmis rezervasyon.</summary>
    private async Task<(SeededHotel Hotel, ReservationDto Reservation)> ConfirmedStayAsync()
    {
        var hotel = await _database.SeedHotelAsync("Otel A");
        var created = await Service(hotel).CreateAsync(new CreateReservationRequest
        {
            RoomId = hotel.RoomIds[0],
            CheckIn = July10,
            CheckOut = July10.AddDays(4),
            Guest = new GuestInput { FullName = "Misafir" }
        });
        return (hotel, await Service(hotel).ConfirmAsync(created.Id));
    }

    [Fact]
    public async Task Onayli_rezervasyona_giris_gunu_check_in_yapilir()
    {
        var (hotel, reservation) = await ConfirmedStayAsync();
        SetToday(July10);

        var result = await Service(hotel).CheckInAsync(reservation.Id);

        Assert.Equal(ReservationStatus.CheckedIn, result.Status);
    }

    [Fact]
    public async Task Gece_yarisindan_sonra_gelen_misafir_otelin_saatine_gore_check_in_yapar()
    {
        // Istanbul UTC+3: yerel 10 Temmuz 01:30 = UTC 9 Temmuz 22:30. "Bugun" UTC'ye gore
        // hesaplansaydi hala 9 Temmuz olurdu ve check-in reddedilirdi.
        var (hotel, reservation) = await ConfirmedStayAsync();
        _clock.SetLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"));
        _clock.SetUtcNow(new DateTimeOffset(2030, 7, 9, 22, 30, 0, TimeSpan.Zero));

        var result = await Service(hotel).CheckInAsync(reservation.Id);

        Assert.Equal(ReservationStatus.CheckedIn, result.Status);
    }

    [Fact]
    public async Task Giris_gununden_once_check_in_yapilamaz()
    {
        var (hotel, reservation) = await ConfirmedStayAsync();
        SetToday(July10.AddDays(-1));

        await Assert.ThrowsAsync<ConflictException>(() => Service(hotel).CheckInAsync(reservation.Id));
    }

    [Fact]
    public async Task Onaylanmamis_rezervasyona_check_in_yapilamaz()
    {
        var hotel = await _database.SeedHotelAsync("Otel A");
        var pending = await Service(hotel).CreateAsync(new CreateReservationRequest
        {
            RoomId = hotel.RoomIds[0], CheckIn = July10, CheckOut = July10.AddDays(1), Guest = new GuestInput { FullName = "M" }
        });
        SetToday(July10);

        await Assert.ThrowsAsync<ConflictException>(() => Service(hotel).CheckInAsync(pending.Id));
    }

    [Fact]
    public async Task Zamaninda_check_out_geceleri_degistirmez()
    {
        var (hotel, reservation) = await ConfirmedStayAsync();
        SetToday(July10);
        await Service(hotel).CheckInAsync(reservation.Id);
        SetToday(July10.AddDays(4));

        var result = await Service(hotel).CheckOutAsync(reservation.Id);

        Assert.Equal(ReservationStatus.CheckedOut, result.Status);
        Assert.Equal(July10.AddDays(4), result.CheckOut);
        await using var db = _database.CreateContext(hotel.Tenant);
        Assert.Equal(4, await db.RoomNights.CountAsync());
    }

    [Fact]
    public async Task Erken_check_out_kalan_geceleri_satisa_acar()
    {
        var (hotel, reservation) = await ConfirmedStayAsync();
        SetToday(July10);
        await Service(hotel).CheckInAsync(reservation.Id);
        SetToday(July10.AddDays(2)); // 4 gecelik konaklamanin 2. gunu ayriliyor

        var result = await Service(hotel).CheckOutAsync(reservation.Id);

        Assert.Equal(July10.AddDays(2), result.CheckOut);
        await using var db = _database.CreateContext(hotel.Tenant);
        Assert.Equal(2, await db.RoomNights.CountAsync());

        // Bosalan 12-14 Temmuz baska misafire satilabilir
        var next = await Service(hotel).CreateAsync(new CreateReservationRequest
        {
            RoomId = hotel.RoomIds[0], CheckIn = July10.AddDays(2), CheckOut = July10.AddDays(4), Guest = new GuestInput { FullName = "Yeni" }
        });
        Assert.Equal(ReservationStatus.Pending, next.Status);
    }

    [Fact]
    public async Task Giris_gunu_ayrilan_misafirin_ilk_gecesi_dolu_sayilir()
    {
        var (hotel, reservation) = await ConfirmedStayAsync();
        SetToday(July10);
        await Service(hotel).CheckInAsync(reservation.Id);

        var result = await Service(hotel).CheckOutAsync(reservation.Id);

        Assert.Equal(July10.AddDays(1), result.CheckOut);
        Assert.Equal(1, result.Nights);
    }

    [Fact]
    public async Task Check_in_yapilmis_rezervasyon_iptal_edilemez()
    {
        var (hotel, reservation) = await ConfirmedStayAsync();
        SetToday(July10);
        await Service(hotel).CheckInAsync(reservation.Id);

        await Assert.ThrowsAsync<ConflictException>(() => Service(hotel).CancelAsync(reservation.Id));
    }

    public void Dispose() => _database.Dispose();
}
