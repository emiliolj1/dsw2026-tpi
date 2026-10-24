using Dsw2026Tpi.Data;
using Dsw2026Tpi.Data.Extensions;
using Dsw2026Tpi.Data.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dsw2026Tpi.Api.Configurations;

public static class PersistenceConfigurationExtensions
{
    public static IServiceCollection AddApplicationPersistence(this IServiceCollection services,
        IConfiguration configuration)
    {
        //Obtener cadena de conexión desde appsettings.json
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        //Agregar contexto (O/RM) y utilizar SQL Server para DB
        services.AddDbContext<Dsw2026TpiDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });

        services.AddDbContext<AuthenticationDbContext>(options =>
        {
            options.UseSqlServer(connectionString);

            options.UseSeeding((context, _) =>
            {
                context.Seedwork<IdentityRole<Guid>>(
                    Path.Combine("Sources", "roles.json"));
            });

            options.UseAsyncSeeding((context, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                context.Seedwork<IdentityRole<Guid>>(
                    Path.Combine("Sources", "roles.json"));

                return Task.CompletedTask;
            });
        });
        return services;
    }
}
