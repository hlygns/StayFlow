namespace StayFlow.Application.Common.Exceptions;

/// <summary>Istege bagli bir ozellik (ör. yapay zeka) calismiyor veya yapilandirilmamis -> 503.</summary>
public class FeatureUnavailableException : Exception
{
    public FeatureUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}
