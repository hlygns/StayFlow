using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Application.Common.Exceptions;

namespace StayFlow.API.Infrastructure;

/// <summary>
/// Application katmanindaki is kurali exception'larini HTTP durum kodlarina cevirir.
/// Boylece servisler HTTP'yi hic bilmez, controller'lar da try/catch ile dolmaz.
/// Cevap formati RFC 7807 ProblemDetails: { status, title, detail }.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Gecersiz istek"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "Yetkisiz"),
            NotFoundException => (StatusCodes.Status404NotFound, "Bulunamadi"),
            ConflictException => (StatusCodes.Status409Conflict, "Cakisma"),
            FeatureUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Servis kullanilamiyor"),
            _ => (StatusCodes.Status500InternalServerError, "Sunucu hatasi")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Beklenmeyen hata");
        }

        httpContext.Response.StatusCode = status;
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                // 500'de ic hata mesajini disari sizdirmiyoruz.
                Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message
            }
        });
    }
}
