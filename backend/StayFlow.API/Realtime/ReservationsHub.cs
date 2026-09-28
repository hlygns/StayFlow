using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StayFlow.Application.Common.Interfaces;

namespace StayFlow.API.Realtime;

/// <summary>
/// Panelin canli baglantisi (WebSocket). Istemci sadece dinler; sunucu rezervasyon degisince haber verir.
/// Her baglanti, JWT'deki hotelId ile kendi otelinin grubuna eklenir: bir otel, baska otelin
/// olaylarini hicbir sekilde alamaz (grup adi istemciden degil, imzali token'dan gelir).
/// </summary>
[Authorize]
public class ReservationsHub : Hub
{
    public const string Path = "/hubs/reservations";
    public const string ReservationChangedEvent = "reservationChanged";

    public static string HotelGroup(Guid hotelId) => $"hotel:{hotelId}";

    public override async Task OnConnectedAsync()
    {
        if (Guid.TryParse(Context.User?.FindFirst("hotelId")?.Value, out var hotelId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HotelGroup(hotelId));
        }
        else
        {
            Context.Abort();
        }

        await base.OnConnectedAsync();
    }
}

/// <param name="OriginClientId">
/// Degisikligi yapan panel sekmesinin kimligi (X-Client-Id basligi). O sekme kendi islemi icin ikinci bir
/// bildirim gostermez. Sunucunun kendi yaptigi degisikliklerde (odeme suresi doldu vb.) null.
/// </param>
public record ReservationChangedMessage(Guid ReservationId, ReservationChange Change, string? OriginClientId = null);

/// <summary>Application katmanindaki IReservationNotifier'in SignalR uygulamasi.</summary>
public class SignalRReservationNotifier : IReservationNotifier
{
    public const string ClientIdHeader = "X-Client-Id";

    private readonly IHubContext<ReservationsHub> _hub;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SignalRReservationNotifier> _logger;

    public SignalRReservationNotifier(IHubContext<ReservationsHub> hub, IHttpContextAccessor httpContextAccessor, ILogger<SignalRReservationNotifier> logger)
    {
        _hub = hub;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task ReservationChangedAsync(Guid hotelId, Guid reservationId, ReservationChange change, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients.Group(ReservationsHub.HotelGroup(hotelId))
                .SendAsync(ReservationsHub.ReservationChangedEvent, new ReservationChangedMessage(reservationId, change, OriginClientId()), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Bildirim gonderilemedi diye rezervasyon islemi basarisiz sayilmasin: veri zaten kaydedildi,
            // panel bir sonraki yenilemede zaten guncel veriyi gorur.
            _logger.LogWarning(ex, "Canli bildirim gonderilemedi {ReservationId}", reservationId);
        }
    }

    // Istemciden gelen deger sadece geri yansitilir (yetki icin kullanilmaz); yine de boyu sinirlanir.
    private string? OriginClientId()
    {
        var value = _httpContextAccessor.HttpContext?.Request.Headers[ClientIdHeader].ToString();
        return string.IsNullOrWhiteSpace(value) || value.Length > 64 ? null : value;
    }
}
