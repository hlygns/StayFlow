using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StayFlow.Application.BackgroundJobs;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Application.Reservations;
using StayFlow.Domain.Enums;
using StayFlow.Tests.Common;

namespace StayFlow.Tests.BackgroundJobs;

public class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

public class RecordingNotifier : IReservationNotifier
{
    public List<(Guid HotelId, Guid ReservationId, ReservationChange Change)> Events { get; } = [];

    public Task ReservationChangedAsync(Guid hotelId, Guid reservationId, ReservationChange change, CancellationToken ct = default)
    {
        Events.Add((hotelId, reservationId, change));
        return Task.CompletedTask;
    }
}

public class BackgroundJobTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2030, 7, 1, 9, 0, 0, TimeSpan.Zero));
    private static readonly DateOnly July10 = new(2030, 7, 10);

    private ReservationService Reservations(SeededHotel hotel) => new(_database.CreateContext(hotel.Tenant), _clock);

    private Task<ReservationDto> BookAsync(SeededHotel hotel, DateOnly checkIn, int nights = 2, string? email = null, int room = 0) =>
        Reservations(hotel).CreateAsync(new CreateReservationRequest
        {
            RoomId = hotel.RoomIds[room],
            CheckIn = checkIn,
            CheckOut = checkIn.AddDays(nights),
            Guest = new GuestInput { FullName = "Misafir", Email = email }
        });

    // ---------- Odeme suresi dolan rezervasyonlar ----------

    [Fact]
    public async Task Odeme_suresi_dolan_bekleyen_rezervasyon_iptal_edilir_ve_geceleri_bosalir()
    {
        var hotel = await _database.SeedHotelAsync("Otel A");
        var reservation = await BookAsync(hotel, July10);
        var notifier = new RecordingNotifier();

        _clock.Advance(TimeSpan.FromMinutes(16)); // 15 dk'lik odeme suresi gecti
        await using var jobDb = _database.CreateContext(new FakeTenantService());
        var cancelled = await new ExpiredReservationCanceller(jobDb, _clock, notifier, NullLogger<ExpiredReservationCanceller>.Instance).RunAsync();

        Assert.Equal(1, cancelled);
        await using var db = _database.CreateContext(hotel.Tenant);
        var saved = await db.Reservations.SingleAsync();
        Assert.Equal(ReservationStatus.Cancelled, saved.Status);
        Assert.NotNull(saved.CancelledAt);
        Assert.Equal(0, await db.RoomNights.CountAsync());
        Assert.Equal((hotel.HotelId, reservation.Id, ReservationChange.AutoCancelled), Assert.Single(notifier.Events));
    }

    [Fact]
    public async Task Suresi_dolmamis_veya_onaylanmis_rezervasyona_dokunulmaz()
    {
        var hotel = await _database.SeedHotelAsync("Otel A", roomNumbers: ["101", "102"]);
        await BookAsync(hotel, July10, room: 0);                                     // sure dolmadi
        var confirmed = await BookAsync(hotel, July10, room: 1);
        await Reservations(hotel).ConfirmAsync(confirmed.Id);                        // onaylandi

        _clock.Advance(TimeSpan.FromMinutes(10));
        await using var jobDb = _database.CreateContext(new FakeTenantService());
        var cancelled = await new ExpiredReservationCanceller(jobDb, _clock, new RecordingNotifier(), NullLogger<ExpiredReservationCanceller>.Instance).RunAsync();

        Assert.Equal(0, cancelled);
        _clock.Advance(TimeSpan.FromMinutes(10)); // artik ilki doldu, onayli olan yine etkilenmez
        cancelled = await new ExpiredReservationCanceller(jobDb, _clock, new RecordingNotifier(), NullLogger<ExpiredReservationCanceller>.Instance).RunAsync();
        Assert.Equal(1, cancelled);
    }

    [Fact]
    public async Task Tum_otellerin_suresi_dolan_rezervasyonlari_iptal_edilir()
    {
        var hotelA = await _database.SeedHotelAsync("Otel A");
        var hotelB = await _database.SeedHotelAsync("Otel B");
        await BookAsync(hotelA, July10);
        await BookAsync(hotelB, July10);

        _clock.Advance(TimeSpan.FromMinutes(20));
        await using var jobDb = _database.CreateContext(new FakeTenantService()); // tenant YOK
        var cancelled = await new ExpiredReservationCanceller(jobDb, _clock, new RecordingNotifier(), NullLogger<ExpiredReservationCanceller>.Instance).RunAsync();

        Assert.Equal(2, cancelled);
    }

    // ---------- Giris hatirlatmasi ----------

    [Fact]
    public async Task Yarin_gelecek_onayli_misafire_bir_kez_hatirlatma_gider()
    {
        var hotel = await _database.SeedHotelAsync("Otel A", roomNumbers: ["101", "102", "103", "104"]);
        var tomorrow = await BookAsync(hotel, July10, email: "ayse@test.com", room: 0);
        var noEmail = await BookAsync(hotel, July10, room: 1);
        var pending = await BookAsync(hotel, July10, email: "bekleyen@test.com", room: 2);
        var later = await BookAsync(hotel, July10.AddDays(3), email: "sonra@test.com", room: 3);
        foreach (var id in new[] { tomorrow.Id, noEmail.Id, later.Id })
        {
            await Reservations(hotel).ConfirmAsync(id);
        }
        _ = pending;

        _clock.SetUtcNow(new DateTimeOffset(2030, 7, 9, 7, 0, 0, TimeSpan.Zero)); // 9 Temmuz sabahi
        var email = new FakeEmailSender();
        await using var jobDb = _database.CreateContext(new FakeTenantService());
        var job = new ArrivalReminderJob(jobDb, email, _clock, NullLogger<ArrivalReminderJob>.Instance);

        Assert.Equal(1, await job.RunAsync());
        Assert.Equal("ayse@test.com", Assert.Single(email.Sent).To);

        // Is tekrar calisirsa (yeniden deneme) ayni misafire ikinci e-posta gitmez
        Assert.Equal(0, await job.RunAsync());
        Assert.Single(email.Sent);
    }

    // ---------- Gun sonu raporu ----------

    [Fact]
    public async Task Gun_sonu_raporu_dogru_sayilari_hesaplar_ve_sahibe_gider()
    {
        var hotel = await _database.SeedHotelAsync("Otel A", roomNumbers: ["101", "102", "103", "104"]);
        await using (var db = _database.CreateContext(hotel.Tenant))
        {
            db.Users.Add(new Domain.Entities.AppUser { Email = "sahip@otel.test", FullName = "Sahip", Role = UserRole.Owner, PasswordHash = "x" });
            db.Users.Add(new Domain.Entities.AppUser { Email = "resepsiyon@otel.test", FullName = "R", Role = UserRole.Reception, PasswordHash = "x" });
            await db.SaveChangesAsync();
        }

        _clock.SetUtcNow(new DateTimeOffset(2030, 7, 10, 7, 0, 0, TimeSpan.Zero)); // 10 Temmuz
        await BookAsync(hotel, July10, nights: 2, room: 0);                         // bugun giris, bu gece dolu (1000 x 2)
        await BookAsync(hotel, July10, nights: 1, room: 1);                         // bugun giris, bu gece dolu (1000)
        var toCancel = await BookAsync(hotel, July10.AddDays(5), nights: 1, room: 2);
        await Reservations(hotel).CancelAsync(toCancel.Id);                          // bugun iptal

        var email = new FakeEmailSender();
        await using var jobDb = _database.CreateContext(new FakeTenantService());
        var job = new DailyReportJob(jobDb, email, _clock, NullLogger<DailyReportJob>.Instance);
        var report = await job.BuildAsync(hotel.HotelId, "Otel A");

        Assert.Equal(4, report.RoomCount);
        Assert.Equal(2, report.OccupiedTonight);
        Assert.Equal(0.5, report.OccupancyRate);
        Assert.Equal(2, report.ArrivalsToday);
        Assert.Equal(2, report.NewReservations);         // iptal edilen sayilmaz
        Assert.Equal(3000m, report.NewReservationRevenue);
        Assert.Equal(1, report.CancellationsToday);

        await job.RunAsync();
        var sent = Assert.Single(email.Sent);             // sadece otel sahibine, resepsiyona degil
        Assert.Equal("sahip@otel.test", sent.To);
        Assert.Contains("%50", sent.HtmlBody);
    }

    public void Dispose() => _database.Dispose();
}
