using Dsw2026Tpi.CrossCutting.Models;
using Dsw2026Tpi.CrossCutting.Resources;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Dsw2026Tpi.Api.Configurations;

public static class HttpErrorConfigurationExtensions
{
    public static IApplicationBuilder UseAppStatusCodePages(
        this IApplicationBuilder app)
    {
        return app.UseStatusCodePages(async statusContext =>
        {
            var context = statusContext.HttpContext;
            var response = context.Response;

            var error = response.StatusCode switch
            {
                StatusCodes.Status400BadRequest => new ErrorResponse(
                    nameof(ErrorCodes.VALIDATION_ERROR),
                    ErrorCodes.VALIDATION_ERROR),

                StatusCodes.Status401Unauthorized => new ErrorResponse(
                    nameof(ErrorCodes.AUTHENTICATION_FAILED),
                    ErrorCodes.AUTHENTICATION_FAILED),

                StatusCodes.Status403Forbidden => new ErrorResponse(
                    nameof(ErrorCodes.AUTHORIZATION_FAILED),
                    ErrorCodes.AUTHORIZATION_FAILED),

                StatusCodes.Status404NotFound => new ErrorResponse(
                    context.GetEndpoint() is null
                        ? "ROUTE_NOT_FOUND"
                        : nameof(ErrorCodes.ENTITY_NOTFOUND),
                    "El recurso solicitado no se encontró."),

                StatusCodes.Status405MethodNotAllowed => new ErrorResponse(
                    "METHOD_NOT_ALLOWED",
                    "El método HTTP no está permitido para este recurso."),

                StatusCodes.Status409Conflict => new ErrorResponse(
                    "CONFLICT",
                    "La operación entra en conflicto con el estado actual."),

                StatusCodes.Status415UnsupportedMediaType =>
                    new ErrorResponse(
                        "UNSUPPORTED_MEDIA_TYPE",
                        "El formato del contenido no está admitido."),

                StatusCodes.Status429TooManyRequests => new ErrorResponse(
                    "RATE_LIMIT_EXCEEDED",
                    ErrorCodes.ResourceManager.GetString(
                        "RATE_LIMIT_EXCEEDED")
                    ?? "Se excedió el límite de solicitudes. " +
                       "Intente nuevamente más tarde."),

                >= StatusCodes.Status500InternalServerError =>
                    new ErrorResponse(
                        nameof(ErrorCodes.UNHANDLED_ERROR),
                        ErrorCodes.UNHANDLED_ERROR),

                _ => new ErrorResponse(
                    "HTTP_ERROR",
                    "La solicitud no pudo completarse.")
            };

            await response.WriteAsJsonAsync(
                error,
                context.RequestAborted);
        });
    }
}
