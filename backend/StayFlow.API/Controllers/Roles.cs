using StayFlow.Domain.Enums;

namespace StayFlow.API.Controllers;

/// <summary>[Authorize(Roles = ...)] sabit string ister; enum adlarindan turetiyoruz ki yazim hatasi olmasin.</summary>
public static class Roles
{
    public const string Owner = nameof(UserRole.Owner);
    public const string Reception = nameof(UserRole.Reception);
    public const string Staff = Owner + "," + Reception;
}
