using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Data;
using Dsw2026Tpi.Data.Options;
using Dsw2026Tpi.Data.Transactions;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class ClinicalWriteScopeSqlTests
    : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public ClinicalWriteScopeSqlTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CompleteCommitsTheWrite()
    {
        var id = Guid.NewGuid();

        await using (var context = _fixture.CreateApplicationContext())
        {
            var factory = CreateFactory(context);

            await using var scope = await factory.BeginAsync();

            context.Set<Speciality>().Add(
                CreateSpeciality(id));

            await scope.CompleteAsync();
        }

        await using var verification =
            _fixture.CreateApplicationContext();

        Assert.True(await verification.Set<Speciality>()
            .AnyAsync(speciality => speciality.Id == id));
    }

    [Fact]
    public async Task DisposeWithoutCompleteRollsBackSavedRows()
    {
        var id = Guid.NewGuid();

        await using (var context = _fixture.CreateApplicationContext())
        {
            var factory = CreateFactory(context);

            await using (var scope = await factory.BeginAsync())
            {
                context.Set<Speciality>().Add(
                    CreateSpeciality(id));

                // Envía el INSERT a SQL, pero no confirma la transacción.
                await context.SaveChangesAsync();

                Assert.True(await context.Set<Speciality>()
                    .AnyAsync(speciality => speciality.Id == id));
            }

            Assert.Empty(context.ChangeTracker.Entries());
        }

        await using var verification =
            _fixture.CreateApplicationContext();

        Assert.False(await verification.Set<Speciality>()
            .AnyAsync(speciality => speciality.Id == id));
    }

    [Fact]
    public async Task IndependentWriterTimesOutAndCanRetryAfterRelease()
    {
        await using var firstContext =
            _fixture.CreateApplicationContext();

        await using var secondContext =
            _fixture.CreateApplicationContext();

        Assert.NotSame(
            firstContext.Database.GetDbConnection(),
            secondContext.Database.GetDbConnection());

        var firstFactory = CreateFactory(firstContext);
        var secondFactory = CreateFactory(
            secondContext,
            timeoutMilliseconds: 200);

        await using (var firstScope = await firstFactory.BeginAsync())
        {
            // El primer escritor conserva el bloqueo mientras el segundo
            // intenta adquirir la misma clave desde otra conexión.
            await Assert.ThrowsAsync<ClinicalWriteConflictException>(
                async () =>
                {
                    await using var unexpectedScope =
                        await secondFactory.BeginAsync();
                });

            Assert.Null(secondContext.Database.CurrentTransaction);
        }

        // Liberado el primer bloqueo, la misma fábrica puede reintentarlo.
        await using var retryScope = await secondFactory.BeginAsync();

        Assert.NotNull(secondContext.Database.CurrentTransaction);

        await retryScope.CompleteAsync();
    }

    private static ClinicalWriteScopeFactory CreateFactory(
        Dsw2026TpiDbContext context,
        int timeoutMilliseconds = 5000)
    {
        return new ClinicalWriteScopeFactory(
            context,
            Options.Create(new ClinicalWriteOptions
            {
                LockTimeoutMilliseconds = timeoutMilliseconds
            }));
    }

    private static Speciality CreateSpeciality(Guid id)
    {
        var now = DateTime.UtcNow;

        return new Speciality(
            $"SQL-{id:N}",
            "Especialidad sintética para pruebas SQL.",
            id)
        {
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
