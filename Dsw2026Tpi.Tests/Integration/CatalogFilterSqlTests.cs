using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class CatalogFilterSqlTests
    : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public CatalogFilterSqlTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(null, 7)]
    [InlineData("Ana", 5)]
    public async Task FilterAndPagination_WorkWithRealSql(
        string? name,
        int expectedTotal)
    {
        var target = new Speciality(
            "Cardiologia",
            "Especialidad SQL buscada");

        var other = new Speciality(
            "Neurologia",
            "Otra especialidad SQL");

        var removed = new Speciality(
            "Pediatria",
            "Especialidad SQL eliminada")
        {
            Deleted = true
        };

        var expectedDoctors = Enumerable.Range(1, 7)
            .Select(index => new Doctor(
                index <= 5 ? "Ana Repetida" : "Bruno Repetido",
                $"SQL-T-{index}",
                target))
            .ToArray();

        var inactive = new Doctor(
            "Ana Inactiva", "SQL-INACTIVE", target);
        inactive.Deactivate();

        var deleted = new Doctor(
            "Ana Eliminada", "SQL-DELETED", target)
        {
            Deleted = true
        };

        var differentSpecialty = new Doctor(
            "Ana Otra", "SQL-OTHER", other);

        var removedSpecialty = new Doctor(
            "Ana Sin Especialidad", "SQL-REMOVED", removed);

        var entities = new List<EntityBase>
        {
            target,
            other,
            removed,
            inactive,
            deleted,
            differentSpecialty,
            removedSpecialty
        };

        entities.AddRange(expectedDoctors);

        var now = DateTime.UtcNow;

        foreach (var entity in entities)
        {
            entity.CreatedAt = now;
            entity.UpdatedAt = now;
        }

        await using (var context = _fixture.CreateApplicationContext())
        {
            context.AddRange(entities);
            await context.SaveChangesAsync();
        }

        var expectedIds = expectedDoctors
            .Where(doctor => name is null || doctor.Name.Contains(name))
            .Select(doctor => doctor.Id)
            .ToHashSet();

        var returnedIds = new List<Guid>();
        const int pageSize = 2;
        var pageCount = (expectedTotal + pageSize - 1) / pageSize;

        for (var pageIndex = 0; pageIndex <= pageCount; pageIndex++)
        {
            await using var scope =
                _fixture.Factory.Services.CreateAsyncScope();

            var service = scope.ServiceProvider
                .GetRequiredService<IDoctorService>();

            var page = await service.GetAll(
                pageSize,
                pageIndex,
                name,
                target.Id);

            Assert.Equal(expectedTotal, page.Total);

            Assert.Equal(
                Math.Min(
                    pageSize,
                    Math.Max(0, expectedTotal - pageIndex * pageSize)),
                page.Data.Count());

            Assert.All(page.Data, doctor =>
                Assert.Equal(target.Id, doctor.Specialty?.Id));

            returnedIds.AddRange(page.Data.Select(doctor => doctor.Id));
        }

        Assert.Equal(expectedTotal, returnedIds.Count);
        Assert.Equal(expectedTotal, returnedIds.Distinct().Count());
        Assert.True(expectedIds.SetEquals(returnedIds));

        await using var verificationScope =
            _fixture.Factory.Services.CreateAsyncScope();

        var verificationService = verificationScope.ServiceProvider
            .GetRequiredService<IDoctorService>();

        var removedPage = await verificationService.GetAll(
            10,
            0,
            specialtyId: removed.Id);

        Assert.Equal(0, removedPage.Total);
        Assert.Empty(removedPage.Data);
    }
}
