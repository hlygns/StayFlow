using StayFlow.Application.Common.Exceptions;

namespace StayFlow.Application.Reservations;

public static class StayDates
{
    public const int MaxNights = 30;

    /// <summary>Musaitlik ve rezervasyon icin ortak tarih kurallari.</summary>
    public static void Validate(DateOnly checkIn, DateOnly checkOut, DateOnly today)
    {
        if (checkOut <= checkIn)
        {
            throw new ValidationException("Cikis tarihi giris tarihinden sonra olmali.");
        }

        if (checkIn < today)
        {
            throw new ValidationException("Gecmis bir tarih icin islem yapilamaz.");
        }

        if (checkOut.DayNumber - checkIn.DayNumber > MaxNights)
        {
            throw new ValidationException($"Tek seferde en fazla {MaxNights} gece secilebilir.");
        }
    }
}
