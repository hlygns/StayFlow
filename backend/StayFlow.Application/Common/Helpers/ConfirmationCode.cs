using System.Security.Cryptography;

namespace StayFlow.Application.Common.Helpers;

/// <summary>
/// Misafire verilen rezervasyon kodu: 8 karakter, karistirilabilecek harf/rakamlar yok (0/O, 1/I/L).
/// 31^8 ~ 850 milyar olasilik; kriptografik rastgelelik: kodlar tahmin edilemez.
/// Tek basina yetki vermez - misafir ayrica e-postasini da girmek zorunda.
/// </summary>
public static class ConfirmationCode
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string New() => RandomNumberGenerator.GetString(Alphabet, 8);

    public static string Normalize(string code) => code.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");
}
