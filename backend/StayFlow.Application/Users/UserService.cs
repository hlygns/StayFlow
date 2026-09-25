using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using StayFlow.Application.Auth;
using StayFlow.Application.Common.Exceptions;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Domain.Entities;
using StayFlow.Domain.Enums;

namespace StayFlow.Application.Users;

public class CreateUserRequest
{
    [Required, MaxLength(200)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Reception;
}

/// <summary>Otel sahibinin kendi personelini (resepsiyon vb.) yonetmesi.</summary>
public class UserService
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IApplicationDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<List<UserDto>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Users
            .OrderBy(u => u.FullName)
            .Select(u => new UserDto(u.Id, u.FullName, u.Email, u.Role, u.HotelId))
            .ToListAsync(ct);

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var email = AuthService.NormalizeEmail(request.Email);

        // E-posta tum sistemde tekil (bkz. AuthService) - baska otelin kullanicilarini da kontrol et.
        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
        {
            throw new ConflictException("Bu e-posta adresi zaten kullaniliyor.");
        }

        // HotelId'yi burada set etmiyoruz: DbContext, giris yapmis kullanicinin otelini otomatik atar.
        var user = new AppUser
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = request.Role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return new UserDto(user.Id, user.FullName, user.Email, user.Role, user.HotelId);
    }
}
