using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Models;
using Dsw2026Tpi.CrossCutting.Resources;

namespace Dsw2026Tpi.Api.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
            when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var level = exception is AppException
                ? LogLevel.Warning
                : LogLevel.Error;

            // No registrar el mensaje o payload de la excepción:
            // podrían contener datos personales o valores SQL.
            _logger.Log(
                level,
                "Solicitud fallida. Método: {Method}. Ruta: {Path}. " +
                "Tipo: {ExceptionType}. TraceId: {TraceId}.",
                context.Request.Method,
                context.Request.Path.Value,
                exception.GetType().Name,
                context.TraceIdentifier);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();

            var error = exception is AppException applicationException
                ? applicationException.Error
                : new ErrorResponse(
                    nameof(ErrorCodes.UNHANDLED_ERROR),
                    ErrorCodes.UNHANDLED_ERROR);

            context.Response.StatusCode = exception switch
            {
                ValidationException =>
                    StatusCodes.Status400BadRequest,

                EntityNotFoundException =>
                    StatusCodes.Status404NotFound,

                ConflictException =>
                    StatusCodes.Status409Conflict,

                AuthenticationException =>
                    StatusCodes.Status401Unauthorized,

                AuthorizationException =>
                    StatusCodes.Status403Forbidden,

                _ => StatusCodes.Status500InternalServerError
            };

            await context.Response.WriteAsJsonAsync(
                error,
                context.RequestAborted);
        }
    }
}
