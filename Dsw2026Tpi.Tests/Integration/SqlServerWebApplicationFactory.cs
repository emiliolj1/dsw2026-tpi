using Dsw2026Tpi.Api;
using Dsw2026Tpi.Data;
using Dsw2026Tpi.Data.Extensions;
using Dsw2026Tpi.Data.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dsw2026Tpi.Tests.Integration;

public sealed class SqlServerWebApplicationFactory
    : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin.sql@example.test";
    public const string AdminPassword = "SqlTests2026!Admin";

    private readonly SqlServerTestConnection _connection;

    public SqlServerWebApplicationFactory(
        SqlServerTestConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] =
                _connection.ConnectionString,
            ["Jwt:Key"] =
                "ClaveExclusivaSqlTests2026_ABCDEFGHIJKLMNOPQRSTUVWXYZ",
            ["Jwt:Issuer"] = "Dsw2026Tpi.SqlTests",
            ["Jwt:Audience"] = "Dsw2026Tpi.SqlTests",
            ["InitialAdmin:Email"] = AdminEmail,
            ["InitialAdmin:Password"] = AdminPassword,
            ["Clinic:TimeZoneId"] = "Argentina Standard Time",
            ["ClinicalWrite:LockTimeoutMilliseconds"] = "1000",
            ["AllowedHosts"] = "localhost"
        };

        foreach (var setting in settings)
        {
            builder.UseSetting(setting.Key, setting.Value);
        }

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            RemoveContext<Dsw2026TpiDbContext>(services);
            RemoveContext<AuthenticationDbContext>(services);

            services.AddDbContext<Dsw2026TpiDbContext>(options =>
                options.UseSqlServer(_connection.ConnectionString));

            services.AddDbContext<AuthenticationDbContext>(options =>
            {
                options.UseSqlServer(_connection.ConnectionString);

                options.UseSeeding((context, _) =>
                    SeedRoles(context));

                options.UseAsyncSeeding((context, _, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    SeedRoles(context);
                    return Task.CompletedTask;
                });
            });
        });
    }

    private static void SeedRoles(DbContext context)
    {
        context.Seedwork<IdentityRole<Guid>>(
            Path.Combine("Sources", "roles.json"));
    }

    private static void RemoveContext<TContext>(
        IServiceCollection services)
        where TContext : DbContext
    {
        services.RemoveAll<TContext>();
        services.RemoveAll<DbContextOptions<TContext>>();
        services.RemoveAll<
            IDbContextOptionsConfiguration<TContext>>();
    }
}
