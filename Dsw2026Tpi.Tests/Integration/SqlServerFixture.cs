using Dsw2026Tpi.Data;
using Dsw2026Tpi.Data.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Dsw2026Tpi.Tests.Integration;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private SqlServerTestConnection? _connection;
    private SqlServerWebApplicationFactory? _factory;
    private HttpClient? _client;

    public SqlServerTestConnection Connection =>
        _connection ?? throw new InvalidOperationException(
            "La conexión SQL todavía no fue inicializada.");

    public SqlServerWebApplicationFactory Factory =>
        _factory ?? throw new InvalidOperationException(
            "La fábrica SQL todavía no fue inicializada.");

    public HttpClient Client =>
        _client ?? throw new InvalidOperationException(
            "El cliente HTTP todavía no fue inicializado.");

    public Dsw2026TpiDbContext CreateApplicationContext()
    {
        var options =
            new DbContextOptionsBuilder<Dsw2026TpiDbContext>()
                .UseSqlServer(Connection.ConnectionString)
                .Options;

        return new Dsw2026TpiDbContext(options);
    }

    public AuthenticationDbContext CreateAuthenticationContext()
    {
        var options =
            new DbContextOptionsBuilder<AuthenticationDbContext>()
                .UseSqlServer(Connection.ConnectionString)
                .Options;

        return new AuthenticationDbContext(options);
    }

    public async Task InitializeAsync()
    {
        _connection = SqlServerTestConnection.Create();

        try
        {
            // Identity primero: el dominio puede referenciar sus usuarios.
            await using (var identity = CreateAuthenticationContext())
            {
                await identity.Database.MigrateAsync();
            }

            await using (var application = CreateApplicationContext())
            {
                await application.Database.MigrateAsync();
            }

            // Al arrancar, Program ejecuta el IdentitySeeder real.
            _factory = new SqlServerWebApplicationFactory(Connection);
            _client = _factory.CreateClient();
        }
        catch (Exception initializationError)
        {
            try
            {
                await DisposeAsync();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(
                    "Falló la preparación SQL y también su limpieza.",
                    initializationError,
                    cleanupError);
            }

            throw new InvalidOperationException(
                "No se pudo preparar la base SQL de pruebas. " +
                $"Revisá {SqlServerTestConnection.EnvironmentVariable}, " +
                "la disponibilidad del motor y las migraciones. " +
                $"Catálogo: {Connection.DatabaseName}.",
                initializationError);
        }
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _client = null;

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            _factory = null;
        }

        if (_connection is null)
        {
            return;
        }

        ValidateCleanupTarget();

        await using var context = CreateApplicationContext();
        await context.Database.EnsureDeletedAsync();
    }

    private void ValidateCleanupTarget()
    {
        var builder = new SqlConnectionStringBuilder(
            Connection.ConnectionString);

        var name = Connection.DatabaseName;
        var prefix = SqlServerTestConnection.DatabasePrefix;

        if (!string.Equals(
                builder.InitialCatalog,
                name,
                StringComparison.Ordinal) ||
            !name.StartsWith(prefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(name[prefix.Length..], "N", out _))
        {
            throw new InvalidOperationException(
                "Limpieza rechazada: el catálogo no coincide " +
                "con la base exclusiva generada para esta prueba.");
        }
    }
}
