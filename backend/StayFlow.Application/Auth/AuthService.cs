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

    public AuthService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        JwtSettings jwtSettings)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _jwtSettings = jwtSettings;
    }

    public async Task<AuthResponse> RegisterHotelAsync(RegisterHotelRequest request, CancellationToken ct = default)
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

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
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

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var principal = _jwtTokenService.GetPrincipalFromExpiredToken(request.AccessToken);
        var userIdClaim = principal?.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedException("Gecersiz token.");
        }

        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null
            || user.RefreshToken != HashToken(request.RefreshToken)
            || user.RefreshTokenExpiresAt is null
            || user.RefreshTokenExpiresAt < DateTime.UtcNow)
        {
            throw new UnauthorizedException("Oturum suresi dolmus, lutfen tekrar giris yapin.");
        }

        // Refresh token rotation: her kullanimda yenisi uretilir, eskisi gecersiz olur.
        // Calinan bir refresh token en fazla bir kere kullanilabilir.
        return await IssueTokensAsync(user, ct);
    }

    public async Task LogoutAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return;
        }

        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<AuthResponse> IssueTokensAsync(AppUser user, CancellationToken ct)
    {
        var (accessToken, expiresAt) = _jwtTokenService.GenerateAccessToken(user);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        // Veritabaninda refresh token'in kendisini degil hash'ini tutuyoruz: DB sizarsa
        // saldirgan bu degerlerle oturum acamaz (sifrelerde yaptigimizin aynisi).
        user.RefreshToken = HashToken(refreshToken);
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(
            accessToken,
            expiresAt,
            refreshToken,
            new UserDto(user.Id, user.FullName, user.Email, user.Role, user.HotelId));
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
