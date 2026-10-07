using Dsw2026Tpi.Data;
using Dsw2026Tpi.Data.Repositories;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ValidationException = Dsw2026Tpi.CrossCutting.Exceptions.ValidationException;

namespace Dsw2026Tpi.Tests.Data;

public class PersistenceEfTests
{
    [Fact]
    public async Task Paginate_WithPageIndexesZeroAndOne_ReturnsDifferentPages()
    {
        await using var context = CreateContext();
        await SeedSpecialities(context);

        var persistence = new PersistenceEf(context);

        var firstPage = await persistence.Paginate<Speciality, string>(
            pageSize: 2,
            pageIndex: 0,
            predicate: speciality => true,
            sortOrder: speciality => speciality.Name);

        var secondPage = await persistence.Paginate<Speciality, string>(
            pageSize: 2,
            pageIndex: 1,
            predicate: speciality => true,
            sortOrder: speciality => speciality.Name);

        Assert.Equal(0, firstPage.PageIndex);
        Assert.Equal(1, secondPage.PageIndex);
        Assert.Equal(5, firstPage.Total);
        Assert.Equal(5, secondPage.Total);

        Assert.Equal(
            new[] { "Cardiología", "Clínica" },
            firstPage.Data.Select(speciality => speciality.Name).ToArray());

        Assert.Equal(
            new[] { "Dermatología", "Neurología" },
            secondPage.Data.Select(speciality => speciality.Name).ToArray());
    }

    [Fact]
    public async Task Paginate_WithNonexistentPage_ReturnsEmptyPage()
    {
        await using var context = CreateContext();
        await SeedSpecialities(context);

        var persistence = new PersistenceEf(context);

        var result = await persistence.Paginate<Speciality, string>(
            pageSize: 2,
            pageIndex: 3,
            predicate: speciality => true,
            sortOrder: speciality => speciality.Name);

        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.PageIndex);
        Assert.Equal(5, result.Total);
        Assert.Empty(result.Data);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(101, 0)]
    [InlineData(10, -1)]
    [InlineData(100, int.MaxValue)]
    [InlineData(2, 1073741824)]
    public async Task Paginate_WithInvalidBounds_ThrowsValidationException(
        int pageSize,
        int pageIndex)
    {
        await using var context = CreateContext();
        var persistence = new PersistenceEf(context);

        await Assert.ThrowsAsync<ValidationException>(async () =>
        {
            await persistence.Paginate<Speciality, string>(
                pageSize,
                pageIndex,
                speciality => true,
                speciality => speciality.Name);
        });
    }

    [Fact]
    public async Task Paginate_WithEqualSortValues_UsesIdAsTieBreaker()
    {
        await using var context = CreateContext();

        var firstId =
            Guid.Parse("00000000-0000-0000-0000-000000000001");

        var secondId =
            Guid.Parse("00000000-0000-0000-0000-000000000002");

        var thirdId =
            Guid.Parse("00000000-0000-0000-0000-000000000003");

        // Datos artificiales para comprobar empates en el repositorio genérico.
        // El orden de inserción difiere deliberadamente del esperado.
        context.AddRange(
            new Speciality("Especialidad", "Descripción de prueba", thirdId),
            new Speciality("Especialidad", "Descripción de prueba", firstId),
            new Speciality("Especialidad", "Descripción de prueba", secondId));

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persistence = new PersistenceEf(context);

        var firstPage = await persistence.Paginate<Speciality, string>(
            pageSize: 2,
            pageIndex: 0,
            predicate: speciality => true,
            sortOrder: speciality => speciality.Name);

        var secondPage = await persistence.Paginate<Speciality, string>(
            pageSize: 2,
            pageIndex: 1,
            predicate: speciality => true,
            sortOrder: speciality => speciality.Name);

        Assert.Equal(3, firstPage.Total);
        Assert.Equal(3, secondPage.Total);

        Assert.Equal(
            new[] { firstId, secondId },
            firstPage.Data.Select(speciality => speciality.Id).ToArray());

        Assert.Equal(
            new[] { thirdId },
            secondPage.Data.Select(speciality => speciality.Id).ToArray());
    }

    [Fact]
    public async Task Paginate_WithMaximumValidOffset_ReturnsEmptyPage()
    {
        await using var context = CreateContext();
        await SeedSpecialities(context);

        var persistence = new PersistenceEf(context);

        var result = await persistence.Paginate<Speciality, string>(
            pageSize: 1,
            pageIndex: int.MaxValue,
            predicate: speciality => true,
            sortOrder: speciality => speciality.Name);

        Assert.Equal(int.MaxValue, result.PageIndex);
        Assert.Equal(5, result.Total);
        Assert.Empty(result.Data);
    }

    private static Dsw2026TpiDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<Dsw2026TpiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new Dsw2026TpiDbContext(options);
    }

    private static async Task SeedSpecialities(
        Dsw2026TpiDbContext context)
    {
        var specialities = new[]
        {
            new Speciality(
                "Pediatría",
                "Atención pediátrica"),
            new Speciality(
                "Cardiología",
                "Atención cardiológica"),
            new Speciality(
                "Neurología",
                "Atención neurológica"),
            new Speciality(
                "Clínica",
                "Atención clínica"),
            new Speciality(
                "Dermatología",
                "Atención dermatológica")
        };

        context.AddRange(specialities);
        await context.SaveChangesAsync();
    }
}
