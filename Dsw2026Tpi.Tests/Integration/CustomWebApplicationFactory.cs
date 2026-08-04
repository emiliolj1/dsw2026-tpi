using Dsw2026Tpi.Api;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Data;
using Dsw2026Tpi.Data.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection; 
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dsw2026Tpi.Tests.Integration;

public sealed class CustomWebApplicationFactory
    : WebApplicationFactory<Program>
{
    internal const string JwtKey =
    "ClaveJwtExclusivaParaPruebasDeIntegracion123456789";

    internal const string JwtIssuer =
        "Dsw2026Tpi.Tests";

    internal const string JwtAudience =
        "Dsw2026Tpi.Tests";
    public CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable(
            "Jwt__Key",
            JwtKey);

        Environment.SetEnvironmentVariable(
            "Jwt__Issuer",
            JwtIssuer);

        Environment.SetEnvironmentVariable(
            "Jwt__Audience",
            JwtAudience);

        Environment.SetEnvironmentVariable(
            "AllowedHosts",
            "localhost");
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            ReplaceDbContext<Dsw2026TpiDbContext>(
                services,
                "Application");

            ReplaceDbContext<AuthenticationDbContext>(
                services,
                "Authentication");

            services.RemoveAll<IInitialAdminSeeder>();

            services.AddSingleton<IInitialAdminSeeder>(
                new NoOpInitialAdminSeeder());
        });
    }

    private static void ReplaceDbContext<TContext>(
        IServiceCollection services,
        string databasePrefix)
        where TContext : DbContext
    {
        services.RemoveAll<TContext>();
        services.RemoveAll<DbContextOptions<TContext>>();

        services.RemoveAll<
            IDbContextOptionsConfiguration<TContext>>();

        var databaseName =
            $"{databasePrefix}-{Guid.NewGuid():N}";

        services.AddDbContext<TContext>(options =>
        {
            options
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(
                        InMemoryEventId.TransactionIgnoredWarning));
        });
    }

    private sealed class NoOpInitialAdminSeeder
        : IInitialAdminSeeder
    {
        public Task SeedAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
