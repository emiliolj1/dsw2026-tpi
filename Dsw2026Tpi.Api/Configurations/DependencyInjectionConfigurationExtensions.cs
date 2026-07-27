using Dsw2026Tpi.Api.Services;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Application.Options;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.Data;
using Dsw2026Tpi.Domain.Interfaces;

namespace Dsw2026Tpi.Api.Configurations;

public static class DependencyInjectionConfigurationExtensions
{
    public static IServiceCollection AddAppDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<InitialAdminOptions>(
            configuration.GetSection(InitialAdminOptions.SectionName));

        services.AddScoped<IInitialAdminSeeder, IdentitySeeder>();
        services.AddScoped<IPersistence, PersistenceEf>();
        services.AddScoped<IAvailabilityPersistence, AvailabilityPersistenceEf>();
        services.AddScoped<IDoctorService, DoctorService>();
        services.AddScoped<ISpecialityService, SpecialityService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<ISignInService, SignInService>();
        services.AddSingleton<JwtService>();

        return services;
    }
}
