using System.Net;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.Data.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class SqlServerInfrastructureTests
    : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public SqlServerInfrastructureTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AppliesMigrationsAndCreatesRealAdministrator()
    {
        await using var application =
            _fixture.CreateApplicationContext();

        await using var identity =
            _fixture.CreateAuthenticationContext();

        Assert.True(application.Database.IsSqlServer());
        Assert.True(identity.Database.IsSqlServer());

        Assert.NotEmpty(
            await application.Database.GetAppliedMigrationsAsync());

        Assert.NotEmpty(
            await identity.Database.GetAppliedMigrationsAsync());

        Assert.Empty(
            await application.Database.GetPendingMigrationsAsync());

        Assert.Empty(
            await identity.Database.GetPendingMigrationsAsync());

        await using var scope =
            _fixture.Factory.Services.CreateAsyncScope();

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var admin = await users.FindByEmailAsync(
            SqlServerWebApplicationFactory.AdminEmail);

        Assert.NotNull(admin);

        Assert.True(await users.CheckPasswordAsync(
            admin,
            SqlServerWebApplicationFactory.AdminPassword));

        Assert.True(await users.IsInRoleAsync(
            admin,
            Roles.Administrator));
    }

    [Fact]
    public async Task StartsApiAndRequiresAuthentication()
    {
        using var response =
            await _fixture.Client.GetAsync("/health-check");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }
}
