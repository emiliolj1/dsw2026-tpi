using Dsw2026Tpi.Application.Options;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.Data.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class IdentitySeederSqlTests
    : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public IdentitySeederSqlTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("", "ValidTest2026!Password")]
    [InlineData("missing-password@example.test", "")]
    [InlineData("   ", "   ")]
    public async Task MissingCredentialsFailExplicitlyWithoutCreatingUsers(
        string email,
        string password)
    {
        await using var before =
            _fixture.CreateAuthenticationContext();

        var initialUsers = await before.Users.CountAsync();

        await using var scope =
            _fixture.Factory.Services.CreateAsyncScope();

        var seeder = CreateSeeder(scope.ServiceProvider, email, password);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => seeder.SeedAsync());

        Assert.Contains("InitialAdmin:Email", error.Message);
        Assert.Contains("InitialAdmin:Password", error.Message);

        await using var verification =
            _fixture.CreateAuthenticationContext();

        Assert.Equal(
            initialUsers,
            await verification.Users.CountAsync());

        var roles = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        Assert.True(await roles.RoleExistsAsync(Roles.Administrator));
        Assert.True(await roles.RoleExistsAsync(Roles.Patient));
    }

    [Fact]
    public async Task InvalidPasswordDoesNotLeaveAnIncompleteAdministrator()
    {
        var email = $"invalid-admin-{Guid.NewGuid():N}@example.test";

        await using var before =
            _fixture.CreateAuthenticationContext();

        var initialUsers = await before.Users.CountAsync();
        var initialAssignments = await before.UserRoles.CountAsync();

        await using var scope =
            _fixture.Factory.Services.CreateAsyncScope();

        var seeder = CreateSeeder(
            scope.ServiceProvider,
            email,
            "short");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => seeder.SeedAsync());

        // Verificación desde otro contexto, sin reutilizar seguimiento.
        await using var verification =
            _fixture.CreateAuthenticationContext();

        Assert.Equal(
            initialUsers,
            await verification.Users.CountAsync());

        Assert.Equal(
            initialAssignments,
            await verification.UserRoles.CountAsync());

        Assert.False(await verification.Users.AnyAsync(
            user => user.NormalizedEmail == email.ToUpperInvariant()));
    }

    [Theory]
    [InlineData("patient")]
    [InlineData("deleted-admin")]
    [InlineData("passwordless-admin")]
    public async Task ExistingIncompatibleIdentityIsNotPromotedOrReactivated(
    string scenario)
    {
        var email = $"seed-collision-{Guid.NewGuid():N}@example.test";
        const string password = "ValidSeeder2026!Password";

        await using var scope =
            _fixture.Factory.Services.CreateAsyncScope();

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            Deleted = scenario == "deleted-admin",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var creation = scenario == "passwordless-admin"
            ? await users.CreateAsync(user)
            : await users.CreateAsync(user, password);

        Assert.True(creation.Succeeded);

        var originalRole = scenario == "patient"
            ? Roles.Patient
            : Roles.Administrator;

        var assignment = await users.AddToRoleAsync(user, originalRole);

        Assert.True(assignment.Succeeded);

        var originalHash = user.PasswordHash;

        var seeder = CreateSeeder(
            scope.ServiceProvider,
            email,
            password);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => seeder.SeedAsync());

        await using var verification =
            _fixture.CreateAuthenticationContext();

        var persisted = await verification.Users
            .AsNoTracking()
            .SingleAsync(current =>
                current.NormalizedEmail == email.ToUpperInvariant());

        Assert.Equal(user.Id, persisted.Id);
        Assert.Equal(originalHash, persisted.PasswordHash);
        Assert.Equal(scenario == "deleted-admin", persisted.Deleted);

        var roles = await (
            from relation in verification.UserRoles
            join role in verification.Roles
                on relation.RoleId equals role.Id
            where relation.UserId == persisted.Id
            select role.Name)
            .ToArrayAsync();

        Assert.Equal(new[] { originalRole }, roles);
    }

    private static IdentitySeeder CreateSeeder(
        IServiceProvider services,
        string email,
        string password)
    {
        return new IdentitySeeder(
            services.GetRequiredService<UserManager<ApplicationUser>>(),
            services.GetRequiredService<
                RoleManager<IdentityRole<Guid>>>(),
            Options.Create(new InitialAdminOptions
            {
                Email = email,
                Password = password
            }),
            NullLogger<IdentitySeeder>.Instance);
    }
}
