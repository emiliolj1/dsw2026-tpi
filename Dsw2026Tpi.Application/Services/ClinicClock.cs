using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Application.Options;
using Microsoft.Extensions.Options;

namespace Dsw2026Tpi.Application.Services;

public sealed class ClinicClock : IClinicClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public ClinicClock(
        TimeProvider timeProvider,
        IOptions<ClinicOptions> options)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);

        _timeProvider = timeProvider;

        var timeZoneId = options.Value.TimeZoneId;

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new OptionsValidationException(
                ClinicOptions.SectionName,
                typeof(ClinicOptions),
                ["Clinic:TimeZoneId es obligatorio."]);
        }

        try
        {
            _timeZone = ResolveTimeZone(timeZoneId);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException
            or InvalidTimeZoneException)
        {
            throw new OptionsValidationException(
                ClinicOptions.SectionName,
                typeof(ClinicOptions),
                [
                    $"No se puede resolver la zona horaria " +
                    $"'{timeZoneId}' configurada en Clinic:TimeZoneId."
                ]);
        }
    }

    public DateTimeOffset GetCurrentLocalDateTime()
    {
        var utcNow = _timeProvider.GetUtcNow();

        return TimeZoneInfo.ConvertTime(utcNow, _timeZone);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Equivalencia explícita para el centro en Argentina.
            // No sustituir una configuración inválida por la zona del host.
            var equivalentId = timeZoneId switch
            {
                "America/Argentina/Buenos_Aires" =>
                    "Argentina Standard Time",

                "Argentina Standard Time" =>
                    "America/Argentina/Buenos_Aires",

                _ => null
            };

            if (equivalentId is null)
            {
                throw;
            }

            return TimeZoneInfo.FindSystemTimeZoneById(equivalentId);
        }
    }
}
