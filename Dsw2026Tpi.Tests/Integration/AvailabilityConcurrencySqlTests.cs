using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Data.Options;
using Dsw2026Tpi.Data.Transactions;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class AvailabilityConcurrencySqlTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;
    public AvailabilityConcurrencySqlTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PostPost_IntervalosSuperpuestos_NuncaGeneraDosReglasConflictivas()
    {
        var id = await SeedDoctorAsync();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<string> TryPost(string start, string end)
        {
            using var scope = _fixture.Factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
            await gate.Task;
            try
            {
                await svc.Create(new AvailabilityModel.Request(id,
                    [new AvailabilityModel.DayRequest("LUNES", start, end)]));
                return "ok";
            }
            catch (ConflictException) { return "conflict"; }
        }
        var one = Task.Run(() => TryPost("09:00", "10:00"));
        var two = Task.Run(() => TryPost("09:15", "10:15"));
        gate.SetResult(true);
        var results = await Task.WhenAll(one, two);
        Assert.Equal(1, results.Count(r => r == "ok"));
        Assert.Equal(1, results.Count(r => r == "conflict"));

        using var verifyScope = _fixture.Factory.Services.CreateScope();
        var service = verifyScope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        Assert.Single(await service.GetMonthlyPlanning(id));
    }

    [Fact]
    public async Task PutPut_SoloQuedaUnaPlanificacionCompleta()
    {
        var id = await SeedDoctorAsync();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Run(string start, string end)
        {
            using var scope = _fixture.Factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
            await gate.Task;
            await svc.Update(new AvailabilityModel.Request(id,
                [new AvailabilityModel.DayRequest("MARTES", start, end)]));
        }
        var first = Task.Run(() => Run("08:00", "09:00"));
        var second = Task.Run(() => Run("10:00", "11:00"));
        gate.SetResult(true);
        await Task.WhenAll(first, second);
        using var verifyScope = _fixture.Factory.Services.CreateScope();
        var service = verifyScope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        var rules = (await service.GetMonthlyPlanning(id)).ToList();
        Assert.Single(rules);
        Assert.Contains(rules[0].StartTime, new[] { "08:00", "10:00" });
    }

    [Fact]
    public async Task Rollback_NoConfirmaBajaLogicaSinCompleteAsync()
    {
        var id = await SeedDoctorAsync();
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
            await svc.Create(new AvailabilityModel.Request(id,
                [new AvailabilityModel.DayRequest("JUEVES", "09:00", "10:00")]));
        }
        Guid ruleId;
        await using (var context = _fixture.CreateApplicationContext())
        {
            ruleId = await context.Set<AvailabilityRule>()
                .Where(r => r.DoctorId == id && !r.Deleted)
                .Select(r => r.Id).SingleAsync();
        }
        await using (var context = _fixture.CreateApplicationContext())
        {
            var factory = new ClinicalWriteScopeFactory(context,
                Options.Create(new ClinicalWriteOptions { LockTimeoutMilliseconds = 5000 }));
            await using (var scope = await factory.BeginAsync())
            {
                var rule = await context.Set<AvailabilityRule>().SingleAsync(r => r.Id == ruleId);
                rule.Deleted = true;
                await context.SaveChangesAsync();
                // Falla simulada: Dispose sin CompleteAsync revierte la escritura SQL.
            }
        }
        await using var verification = _fixture.CreateApplicationContext();
        Assert.False((await verification.Set<AvailabilityRule>()
            .SingleAsync(r => r.Id == ruleId)).Deleted);
    }

    private async Task<Guid> SeedDoctorAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var speciality = new Speciality($"Esp {suffix}", "Especialidad de prueba");
        var doctor = new Doctor("Médico concurrente", $"C-{suffix}", speciality);
        await using var context = _fixture.CreateApplicationContext();
        context.AddRange(speciality, doctor);
        await context.SaveChangesAsync();
        return doctor.Id;
    }
}
