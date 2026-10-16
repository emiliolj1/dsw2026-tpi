using Microsoft.Data.SqlClient;

namespace Dsw2026Tpi.Tests.Integration;

public sealed class SqlServerTestConnectionTests
{
    [Theory]
    [InlineData("Dsw2026Tpi")]
    [InlineData("master")]
    [InlineData("")]
    public void RejectsNonTestCatalogs(string catalog)
    {
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB",
            InitialCatalog = catalog,
            IntegratedSecurity = true
        };

        Assert.Throws<InvalidOperationException>(() =>
            SqlServerTestConnection.FromConnectionString(
                connection.ConnectionString));
    }

    [Fact]
    public void GeneratesUniqueDatabaseNames()
    {
        const string configuredConnection =
            @"Server=(localdb)\MSSQLLocalDB;" +
            "Database=Dsw2026Tpi_Test_Config;" +
            "Integrated Security=true;TrustServerCertificate=true;";

        var first = SqlServerTestConnection.FromConnectionString(
            configuredConnection);

        var second = SqlServerTestConnection.FromConnectionString(
            configuredConnection);

        Assert.NotEqual(first.DatabaseName, second.DatabaseName);

        Assert.StartsWith(
            SqlServerTestConnection.DatabasePrefix,
            first.DatabaseName);

        var parsed = new SqlConnectionStringBuilder(
            first.ConnectionString);

        Assert.Equal(first.DatabaseName, parsed.InitialCatalog);
        Assert.Equal(@"(localdb)\MSSQLLocalDB", parsed.DataSource);
        Assert.True(parsed.IntegratedSecurity);
        Assert.False(parsed.Pooling);
    }

    [Fact]
    public void RejectsAttachedDatabaseFiles()
    {
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB",
            InitialCatalog = "Dsw2026Tpi_Test_Config",
            IntegratedSecurity = true,
            AttachDBFilename = @"C:\Temp\existing.mdf"
        };

        Assert.Throws<InvalidOperationException>(() =>
            SqlServerTestConnection.FromConnectionString(
                connection.ConnectionString));
    }
}
