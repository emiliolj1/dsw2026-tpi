namespace Dsw2026Tpi.Application.Interfaces;

public interface IInitialAdminSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
