namespace StayFlow.Application.Common.Interfaces;

public enum ReservationChange
{
    Created,
    Confirmed,
    CheckedIn,
    CheckedOut,
    Cancelled,
    AutoCancelled
}

/// <summary>
/// Bir rezervasyon degistiginde "ilgilenenlere" haber verir: otelin acik paneli (SignalR) ve
/// ileride misafirin telefonu (push bildirim). Application katmani SignalR'i bilmez - sadece bu sozlesmeyi.
/// </summary>
public interface IReservationNotifier
{
    Task ReservationChangedAsync(Guid hotelId, Guid reservationId, ReservationChange change, CancellationToken ct = default);
}

/// <summary>Bildirim altyapisi olmayan yerler (testler, arka plan isleri icin varsayilan) icin bos uygulama.</summary>
public sealed class NullReservationNotifier : IReservationNotifier
{
    public static readonly NullReservationNotifier Instance = new();

    public Task ReservationChangedAsync(Guid hotelId, Guid reservationId, ReservationChange change, CancellationToken ct = default) =>
        Task.CompletedTask;
}
