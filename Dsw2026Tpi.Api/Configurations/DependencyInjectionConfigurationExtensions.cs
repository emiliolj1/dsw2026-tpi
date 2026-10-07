using Dsw2026Tpi.Api.Services;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Application.Options;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.Data;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.Extensions.Options;
using System.Globalization;
using Dsw2026Tpi.Data.Repositories;
using Dsw2026Tpi.Data.Options;
using Dsw2026Tpi.Data.Transactions;

namespace Dsw2026Tpi.Api.Configurations;

public static class DependencyInjectionConfigurationExtensions
{
    public static IServiceCollection AddAppDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<TimeProvider>(TimeProvider.System);

        services.AddOptions<ClinicOptions>()
            .Bind(configuration.GetSection(ClinicOptions.SectionName))
            .Validate(options =>
            {
                try
                {
                    // Reutiliza la resolución y validación del reloj.
                    _ = new ClinicClock(
                        TimeProvider.System,
                        Options.Create(options));

                    return true;
                }
                catch (OptionsValidationException)
                {
                    return false;
                }
            }, "Clinic:TimeZoneId debe identificar una zona horaria válida y disponible.")
            .ValidateOnStart();

        services.AddSingleton<IClinicClock, ClinicClock>();
        services.Configure<InitialAdminOptions>(configuration.GetSection(InitialAdminOptions.SectionName));
        services.AddOptions<NonWorkingDaysOptions>().Bind(configuration.GetSection(NonWorkingDaysOptions.SectionName))
            .Validate(options => options.Dates is not null && options.Dates.All(date => DateOnly
            .TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)), "Todas las fechas no laborables deben utilizar el formato yyyy-MM-dd.")
            .ValidateOnStart();
        services.AddScoped<IInitialAdminSeeder, IdentitySeeder>();
        services.AddOptions<ClinicalWriteOptions>()
            .Bind(configuration.GetSection(ClinicalWriteOptions.SectionName))
            .Validate(
                options => options.LockTimeoutMilliseconds is >= 0 and <= 60000,
                "ClinicalWrite:LockTimeoutMilliseconds debe estar entre 0 y 60000.")
            .ValidateOnStart();

        services.AddScoped<
            IClinicalWriteScopeFactory,
            ClinicalWriteScopeFactory>();
        services.AddScoped<IPersistence, PersistenceEf>();
        services.AddScoped<IAvailabilityPersistence, AvailabilityPersistenceEf>();
        services.AddScoped<IAppointmentPersistence, AppointmentPersistenceEf>();
        services.AddScoped<IDoctorService, DoctorService>();

        services.AddScoped<ISpecialityService, SpecialityService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<ISignInService, SignInService>();

        services.AddSingleton<JwtService>();
        services.AddSingleton<ITokenRevocationService,
            TokenRevocationService>();

        return services;
    }
}
