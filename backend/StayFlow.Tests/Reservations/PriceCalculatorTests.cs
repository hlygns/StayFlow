using StayFlow.Application.Reservations;
using StayFlow.Domain.Entities;

namespace StayFlow.Tests.Reservations;

public class PriceCalculatorTests
{
    private static readonly SeasonalPrice Summer = new()
    {
        StartDate = new DateOnly(2027, 6, 1),
        EndDate = new DateOnly(2027, 8, 31),
        PricePerNight = 3000m
    };

    [Fact]
    public void Sezon_disinda_baz_fiyat_kullanilir()
    {
        var total = PriceCalculator.CalculateTotal(1000m, [Summer], new(2027, 5, 1), new(2027, 5, 4));

        Assert.Equal(3000m, total); // 3 gece x 1000
    }

    [Fact]
    public void Sezon_sinirini_asan_konaklamada_her_gece_kendi_fiyatini_alir()
    {
        // 30 May, 31 May -> baz (2 x 1000) ; 1 Haz -> sezon (3000)
        var total = PriceCalculator.CalculateTotal(1000m, [Summer], new(2027, 5, 30), new(2027, 6, 2));

        Assert.Equal(5000m, total);
    }

    [Fact]
    public void Sezonun_bitis_gunu_dahildir_cikis_gunu_ucretlendirilmez()
    {
        // 31 Agu -> sezon (3000), 1 Eyl -> baz (1000). 2 Eyl cikis gunu, gece sayilmaz.
        var total = PriceCalculator.CalculateTotal(1000m, [Summer], new(2027, 8, 31), new(2027, 9, 2));

        Assert.Equal(4000m, total);
    }
}
