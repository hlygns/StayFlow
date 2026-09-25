namespace StayFlow.Application.Common.Exceptions;

/// <summary>Istenen kayit bulunamadi -> API katmaninda 404'e cevrilir.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
