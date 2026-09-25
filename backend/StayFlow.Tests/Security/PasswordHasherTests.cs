using StayFlow.Application.Common.Helpers;
using StayFlow.Infrastructure.Security;

namespace StayFlow.Tests.Security;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Dogru_sifre_dogrulanir_yanlis_sifre_reddedilir()
    {
        var hash = _hasher.Hash("Parola123!");

        Assert.True(_hasher.Verify("Parola123!", hash));
        Assert.False(_hasher.Verify("parola123!", hash));
    }

    [Fact]
    public void Ayni_sifre_her_seferinde_farkli_hash_uretir()
    {
        Assert.NotEqual(_hasher.Hash("Parola123!"), _hasher.Hash("Parola123!"));
    }

    [Fact]
    public void Bozuk_hash_formati_false_doner()
    {
        Assert.False(_hasher.Verify("Parola123!", "bozuk-deger"));
    }

    [Theory]
    [InlineData("Güneş Butik Otel", "gunes-butik-otel")]
    [InlineData("  İstanbul  Çırağan!! ", "istanbul-ciragan")]
    [InlineData("Otel 2027", "otel-2027")]
    public void Slug_turkce_karakterleri_donusturur(string input, string expected)
    {
        Assert.Equal(expected, SlugGenerator.Generate(input));
    }
}
