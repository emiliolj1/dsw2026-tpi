using Dsw2026Tpi.Data;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dsw2026Tpi.Tests.Data;

public class PersistenceEfTests
{
    [Fact]
    public async Task Paginate_WithPageIndexesZeroAndOne_ReturnsDifferentPages()
    {
        await using var context = CreateContext();

        await SeedSpecialities(context);

        var persistence = new PersistenceEf(context);

        var firstPage =
            await persistence.Paginate<Speciality, string>(
                pageSize: 2,
                pageIndex: 0,
                predicate: speciality => true,
                sortOrder: speciality => speciality.Name);

        var secondPage =
            await persistence.Paginate<Speciality, string>(
                pageSize: 2,
                pageIndex: 1,
                predicate: speciality => true,
                sortOrder: speciality => speciality.Name);

        Assert.Equal(0, firstPage.PageIndex);
        Assert.Equal(1, secondPage.PageIndex);
        Assert.Equal(5, firstPage.Total);
        Assert.Equal(5, secondPage.Total);

        Assert.Equal(
            ["Cardiología", "Clínica"],
            firstPage.Data.Select(
                speciality => speciality.Name));

        Assert.Equal(
            ["Dermatología", "Neurología"],
            secondPage.Data.Select(
                speciality => speciality.Name));
    }

    [Fact]
    public async Task Paginate_WithNonexistentPage_ReturnsEmptyPage()
    {
        await using var context = CreateContext();

        await SeedSpecialities(context);

        var persistence = new PersistenceEf(context);

        var result =
            await persistence.Paginate<Speciality, string>(
                pageSize: 2,
                pageIndex: 3,
                predicate: speciality => true,
                sortOrder: speciality => speciality.Name);

        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.PageIndex);
        Assert.Equal(5, result.Total);
        Assert.Empty(result.Data);
    }

    private static Dsw2026TpiDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<Dsw2026TpiDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString())
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
