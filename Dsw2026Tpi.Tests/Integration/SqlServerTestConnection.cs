using Microsoft.Data.SqlClient;

namespace Dsw2026Tpi.Tests.Integration;

public sealed class SqlServerTestConnection
{
    public const string EnvironmentVariable =
        "DSW2026_TEST_SQL_CONNECTION";

    public const string DatabasePrefix = "Dsw2026Tpi_Test_";

    public string ConnectionString { get; }
    public string DatabaseName { get; }

    private SqlServerTestConnection(
        string connectionString,
        string databaseName)
    {
        ConnectionString = connectionString;
        DatabaseName = databaseName;
    }

    public static SqlServerTestConnection Create()
    {
        var configuredConnection =
            Environment.GetEnvironmentVariable(EnvironmentVariable);

        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            throw new InvalidOperationException(
                $"Falta configurar {EnvironmentVariable}. " +
                "Las pruebas SQL requieren una conexión exclusiva de pruebas.");
        }

        return FromConnectionString(configuredConnection);
    }

    public static SqlServerTestConnection FromConnectionString(
        string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);

        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            throw new InvalidOperationException(
                "La conexión de pruebas debe indicar un servidor SQL.");
        }

        if (!builder.InitialCatalog.StartsWith(
                DatabasePrefix,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"El catálogo configurado debe comenzar con {DatabasePrefix}.");
        }

        if (!string.IsNullOrWhiteSpace(builder.AttachDBFilename))
        {
            throw new InvalidOperationException(
                "Las pruebas no admiten archivos de base adjuntos.");
        }

        var databaseName = $"{DatabasePrefix}{Guid.NewGuid():N}";

        builder.InitialCatalog = databaseName;
        builder.Pooling = false;

        return new SqlServerTestConnection(
            builder.ConnectionString,
            databaseName);
    }
}
