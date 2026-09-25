namespace StayFlow.Application.Common.Exceptions;

/// <summary>Kimlik dogrulanamadi (yanlis sifre, gecersiz refresh token) -> API katmaninda 401'e cevrilir.</summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}
