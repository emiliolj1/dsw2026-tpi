using Dsw2026Tpi.Application.Options;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.CrossCutting.Models;
using Dsw2026Tpi.CrossCutting.Resources;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Dsw2026Tpi.Api.Configurations;

public static class RateLimitingConfigurationExtensions
{
    private const string RateLimitExceededCode =
        "RATE_LIMIT_EXCEEDED";

    public static IServiceCollection AddAppRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration
            .GetSection(RateLimitingOptions.SectionName)
            .Get<RateLimitingOptions>()
            ?? throw new InvalidOperationException(
                $"Falta la sección {RateLimitingOptions.SectionName}.");

        Validate(settings);

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter =
                PartitionedRateLimiter.Create<HttpContext, string>(
                    context => CreatePartition(
                        GetUserOrIpPartitionKey(context),
                        settings.General));

            options.AddPolicy<string>(
                RateLimitPolicies.AdminLogin,
                context => CreatePartition(
                    GetIpPartitionKey(context),
                    settings.AdminLogin));

            options.AddPolicy<string>(
                RateLimitPolicies.PatientLogin,
                context => CreatePartition(
                    GetIpPartitionKey(context),
                    settings.PatientLogin));

            options.AddPolicy<string>(
                RateLimitPolicies.AppointmentBooking,
                context => CreatePartition(
                    GetUserOrIpPartitionKey(context),
                    settings.AppointmentBooking));

            options.RejectionStatusCode =
                StatusCodes.Status429TooManyRequests;

            options.OnRejected =
                async (context, cancellationToken) =>
                {
                    var httpContext = context.HttpContext;

                    var partitionKey =
                        GetUserOrIpPartitionKey(httpContext);

                    var logger = httpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("RateLimiting");

                    logger.LogWarning(
                        "Solicitud rechazada por rate limiting. " +
                        "Ruta: {Path}. Partición: {PartitionKey}",
                        httpContext.Request.Path.Value,
                        partitionKey);

                    httpContext.Response.StatusCode =
                        StatusCodes.Status429TooManyRequests;

                    var message =
                        ErrorCodes.ResourceManager.GetString(
                            RateLimitExceededCode)
                        ?? "Se excedió el límite de solicitudes. " +
                           "Intente nuevamente más tarde.";

                    var error = new ErrorResponse(
                        RateLimitExceededCode,
                        message);

                    await httpContext.Response.WriteAsJsonAsync(
                        error,
                        cancellationToken);
                };
        });

        return services;
    }

    private static RateLimitPartition<string> CreatePartition(
        string partitionKey,
        RateLimitPolicyOptions settings)
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = settings.PermitLimit,
                Window = TimeSpan.FromSeconds(
                    settings.WindowSeconds),
                QueueProcessingOrder =
                    QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    }

    private static string GetIpPartitionKey(
        HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return $"ip:{ip}";
    }

    private static string GetUserOrIpPartitionKey(
        HttpContext context)
    {
        var userId = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return string.IsNullOrWhiteSpace(userId)
            ? GetIpPartitionKey(context)
            : $"user:{userId}";
    }

    private static void Validate(
        RateLimitingOptions settings)
    {
        ValidatePolicy(
            nameof(settings.AdminLogin),
            settings.AdminLogin);

        ValidatePolicy(
            nameof(settings.PatientLogin),
            settings.PatientLogin);

        ValidatePolicy(
            nameof(settings.AppointmentBooking),
            settings.AppointmentBooking);

        ValidatePolicy(
            nameof(settings.General),
            settings.General);
    }

    private static void ValidatePolicy(
        string name,
        RateLimitPolicyOptions settings)
    {
        if (settings.PermitLimit <= 0 ||
            settings.WindowSeconds <= 0)
        {
            throw new InvalidOperationException(
                $"La configuración RateLimiting:{name} " +
                "no es válida.");
        }
    }
}
