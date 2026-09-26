using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StayFlow.Application.Common.Exceptions;
using StayFlow.Application.Common.Helpers;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Application.Common.Models;
using StayFlow.Domain.Entities;
using StayFlow.Domain.Enums;

namespace StayFlow.Application.Auth;

public class AuthService
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtSettings _jwtSettings;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Yeni token uretildikten sonra eski token'in hala kabul edildigi sure. Iki sekme ayni anda
    /// yenileme yaparsa ikincisi oturumdan dusmesin diye. Kisa tutuluyor: calinan eski bir token
    /// bu pencere disinda ise yaramaz.
    /// </summary>
    public static readonly TimeSpan RotationGracePeriod = TimeSpan.FromSeconds(30);

    public AuthService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        JwtSettings jwtSettings,
        TimeProvider timeProvider)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _jwtSettings = jwtSettings;
        _timeProvider = timeProvider;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<AuthResult> RegisterHotelAsync(RegisterHotelRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.OwnerEmail);

        // Giris ekraninda otel secilmiyor, sadece e-posta + sifre soruluyor. Bu yuzden
        // e-posta tum sistemde tekil olmali. Kayit aninda henuz tenant yok -> filtreyi kapatiyoruz.
        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
        {
            throw new ConflictException("Bu e-posta adresi zaten kullaniliyor.");
        }

        var hotel = new Hotel
        {
            Name = request.HotelName.Trim(),
            Slug = await GenerateUniqueSlugAsync(request.HotelName, ct),
            City = request.City.Trim(),
            Address = request.Address.Trim(),
            Phone = request.Phone.Trim(),
            Email = request.HotelEmail?.Trim() ?? string.Empty
        };

        var owner = new AppUser
        {
            HotelId = hotel.Id,
            Email = email,
            FullName = request.OwnerFullName.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRole.Owner
        };

        _db.Hotels.Add(hotel);
        _db.Users.Add(owner);

        // Hotel + Owner tek SaveChanges -> tek transaction: ya ikisi de kaydedilir ya hicbiri.
        return await IssueTokensAsync(owner, ct);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _db.Users.IgnoreQueryFilters()
            .Include(u => u.Hotel)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        // "Kullanici yok" ile "sifre yanlis" ayni mesaji verir: saldirgan hangi e-postalarin
        // kayitli oldugunu deneme-yanilma ile ogrenemesin.
        if (user is null || !user.Hotel.IsActive || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("E-posta veya sifre hatali.");
        }

        return await IssueTokensAsync(user, ct);
    }

    /// <summary>
    /// Refresh token (cookie'den gelir) ile yeni bir access token + yeni bir refresh token uretir.
    /// Sayfa yenilendiginde de bu cagrilir: access token sadece bellekte tutuldugu icin kaybolur.
    /// </summary>
    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new UnauthorizedException("Oturum bulunamadi, lutfen giris yapin.");
        }

        var hash = HashToken(refreshToken);
        var user = await _db.Users.IgnoreQueryFilters()
            .Include(u => u.Hotel)
            .FirstOrDefaultAsync(u => u.RefreshToken == hash || u.PreviousRefreshToken == hash, ct);

        var isCurrent = user?.RefreshToken == hash;
        var isRecentPrevious = user?.PreviousRefreshToken == hash
            && user.RefreshTokenRotatedAt > UtcNow - RotationGracePeriod;

        if (user is null
            || !(isCurrent || isRecentPrevious)
            || user.RefreshTokenExpiresAt is null
            || user.RefreshTokenExpiresAt < UtcNow
            || !user.Hotel.IsActive)
        {
            throw new UnauthorizedException("Oturum suresi dolmus, lutfen tekrar giris yapin.");
        }

        // Refresh token rotation: her kullanimda yenisi uretilir, eskisi (kisa tolerans suresi
        // disinda) gecersiz olur. Calinan bir refresh token pratikte bir kere kullanilabilir.
        return await IssueTokensAsync(user, ct);
    }

    /// <summary>Cikis: bu refresh token'i (ve oncekini) gecersiz kilar.</summary>
    public async Task LogoutAsync(string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        var hash = HashToken(refreshToken);
        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.RefreshToken == hash || u.PreviousRefreshToken == hash, ct);
        if (user is null)
        {
            return;
        }

        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;
        user.PreviousRefreshToken = null;
        user.RefreshTokenRotatedAt = null;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<AuthResult> IssueTokensAsync(AppUser user, CancellationToken ct)
    {
        var (accessToken, expiresAt) = _jwtTokenService.GenerateAccessToken(user);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();
        var refreshExpiresAt = UtcNow.AddDays(_jwtSettings.RefreshTokenDays);

        // Veritabaninda refresh token'in kendisini degil hash'ini tutuyoruz: DB sizarsa
        // saldirgan bu degerlerle oturum acamaz (sifrelerde yaptigimizin aynisi).
        user.PreviousRefreshToken = user.RefreshToken;
        user.RefreshTokenRotatedAt = UtcNow;
        user.RefreshToken = HashToken(refreshToken);
        user.RefreshTokenExpiresAt = refreshExpiresAt;
        await _db.SaveChangesAsync(ct);

        var response = new AuthResponse(
            accessToken,
            expiresAt,
            new UserDto(user.Id, user.FullName, user.Email, user.Role, user.HotelId));
        return new AuthResult(response, refreshToken, refreshExpiresAt);
    }

    private async Task<string> GenerateUniqueSlugAsync(string hotelName, CancellationToken ct)
    {
        var baseSlug = SlugGenerator.Generate(hotelName);
        if (baseSlug.Length == 0)
        {
            baseSlug = "otel";
        }

        var slug = baseSlug;
        var suffix = 2;
        while (await _db.Hotels.AnyAsync(h => h.Slug == slug, ct))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }

    internal static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string HashToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
