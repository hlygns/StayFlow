using StayFlow.Domain.Entities;

namespace StayFlow.Application.Reservations;

/// <summary>
/// Konaklama fiyati = her gece icin (o geceyi kapsayan sezon fiyati ?? oda tipinin baz fiyati).
/// Veritabanina dokunmayan saf (pure) bir fonksiyon: kolayca unit test edilir.
/// </summary>
public static class PriceCalculator
{
    public static decimal CalculateTotal(
        decimal basePrice,
        IEnumerable<SeasonalPrice> seasonalPrices,
        DateOnly checkIn,
        DateOnly checkOut)
    {
        var prices = seasonalPrices.ToList();
        var total = 0m;

        // Konaklanan geceler: checkIn dahil, checkOut haric (checkOut sabahi oda bosaltilir).
        for (var night = checkIn; night < checkOut; night = night.AddDays(1))
        {
            var season = prices.FirstOrDefault(sp => sp.StartDate <= night && night <= sp.EndDate);
            total += season?.PricePerNight ?? basePrice;
        }

        return total;
    }

    public static IEnumerable<DateOnly> Nights(DateOnly checkIn, DateOnly checkOut)
    {
        for (var night = checkIn; night < checkOut; night = night.AddDays(1))
        {
            yield return night;
        }
    }
}
