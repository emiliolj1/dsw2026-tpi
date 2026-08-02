using Dsw2026Tpi.Api.Services;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Application.Options;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.Data;
using Dsw2026Tpi.Domain.Interfaces;
using System.Globalization;
using Dsw2026Tpi.Data.Repositories;

namespace Dsw2026Tpi.Api.Configurations;

public static class DependencyInjectionConfigurationExtensions
{
    public static IServiceCollection AddAppDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<InitialAdminOptions>(configuration.GetSection(InitialAdminOptions.SectionName));
        services.AddOptions<NonWorkingDaysOptions>().Bind(configuration.GetSection(NonWorkingDaysOptions.SectionName))
            .Validate(options => options.Dates is not null && options.Dates.All(date => DateOnly
            .TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)), "Todas las fechas no laborables deben utilizar el formato yyyy-MM-dd.")
            .ValidateOnStart();
        services.AddScoped<IInitialAdminSeeder, IdentitySeeder>();

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

        return services;
    }
}
