using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Extensions;

namespace Jdice.Api.Setup;

/// <summary>
/// Traduz corpo de requisição inválido em 400. O ASP.NET sinaliza esse caso com
/// <see cref="BadHttpRequestException"/>, que sem tratamento sobe até o handler
/// genérico e vira 500 — culpando o servidor por um erro de quem chamou.
/// </summary>
public sealed class BadRequestExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequest)
        {
            return false;
        }

        httpContext.Response.StatusCode = badRequest.StatusCode;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = badRequest.StatusCode,
                Title = "Requisição inválida",
                Detail = badRequest.Message
            }
        });
    }
}
