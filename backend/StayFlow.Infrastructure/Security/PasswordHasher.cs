using System.Security.Cryptography;
using StayFlow.Application.Common.Interfaces;

namespace StayFlow.Infrastructure.Security;

/// <summary>
/// PBKDF2 tabanli sifre hashleme. Her sifre icin rastgele bir "salt" uretilir, boylece
/// ayni sifreyi kullanan iki kullanicinin veritabanindaki hash'i farkli olur
/// (hazir "rainbow table" saldirilarina kapali). Sonuc "iterasyon.salt.hash" seklinde
/// tek bir string olarak saklanir, boylece ileride iterasyon sayisini artirmak istersek
/// eski kayitlar da dogru dogrulanabilir.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hashedPassword)
    {
        var parts = hashedPassword.Split('.', 3);
        if (parts.Length != 3)
        {
            return false;
        }

        var iterations = int.Parse(parts[0]);
        var salt = Convert.FromBase64String(parts[1]);
        var expectedHash = Convert.FromBase64String(parts[2]);

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSize);

        // Sabit-zamanli karsilastirma: normal '==' kullansaydik, hash'in ilk byte'lari
        // tutmadiginda daha erken donerdi - bu da saldirgana "zamanlama" bilgisi sizdirir.
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
