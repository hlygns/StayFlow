namespace StayFlow.Application.Common.Exceptions;

/// <summary>DataAnnotations ile ifade edilemeyen is kurali dogrulamalari (ör. tarih araligi) -> 400.</summary>
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
