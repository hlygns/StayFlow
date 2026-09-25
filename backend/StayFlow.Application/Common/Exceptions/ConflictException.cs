namespace StayFlow.Application.Common.Exceptions;

/// <summary>Is kurali cakismasi (ör. cifte rezervasyon, kullanilan e-posta) -> API katmaninda 409'a cevrilir.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
