using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Dsw2026Tpi.Data.Repositories;

public class AvailabilityPersistenceEf : IAvailabilityPersistence
{
    private readonly Dsw2026TpiDbContext _context;

    public AvailabilityPersistenceEf(Dsw2026TpiDbContext context) => _context = context;

    public async Task<IReadOnlyCollection<Availability>> GetByDoctorAndPeriod(
        Guid doctorId, DateOnly fromInclusive, DateOnly toExclusive)
    {
        return await _context.Set<Availability>()
            .AsNoTracking()
            .Where(s => !s.Deleted && s.DoctorId == doctorId &&
                        s.Date >= fromInclusive && s.Date < toExclusive)
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .ToListAsync();
    }

    public async Task<IReadOnlyCollection<AvailabilityRule>> GetRulesByDoctorAndMonth(
        Guid doctorId, int year, int month)
    {
        return await _context.Set<AvailabilityRule>()
            .AsNoTracking()
            .Where(r => !r.Deleted && r.DoctorId == doctorId &&
                        r.Year == year && r.Month == month)
            .OrderBy(r => r.DayOfWeek).ThenBy(r => r.StartTime)
            .ToListAsync();
    }

    public async Task AddRules(IReadOnlyCollection<AvailabilityRule> rules)
    {
        if (rules.Count == 0) return;
        EnsureWriteScope();
        var now = DateTime.UtcNow;
        foreach (var rule in rules)
        {
            rule.CreatedAt = now;
            rule.UpdatedAt = now;
            rule.Deleted = false;
        }
        await _context.Set<AvailabilityRule>().AddRangeAsync(rules);
        await SaveChangesHandlingDuplicateAsync();
    }

    public async Task SoftDeleteRules(IReadOnlyCollection<Guid> ruleIds)
    {
        if (ruleIds.Count == 0) return;
        EnsureWriteScope();
        var ids = ruleIds.ToHashSet();
        var rules = await _context.Set<AvailabilityRule>()
            .Where(r => ids.Contains(r.Id) && !r.Deleted).ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var rule in rules)
        {
            rule.Deleted = true;
            rule.UpdatedAt = now;
        }
        await SaveChangesHandlingDuplicateAsync();
    }

    public async Task AddRange(IReadOnlyCollection<Availability> availabilities)
    {
        if (availabilities.Count == 0) return;
        EnsureWriteScope();
        var now = DateTime.UtcNow;
        foreach (var slot in availabilities)
        {
            if (slot.AvailabilityRuleId is null)
                throw new InvalidOperationException("Los slots nuevos requieren AvailabilityRuleId.");
            slot.CreatedAt = now;
            slot.UpdatedAt = now;
            slot.Deleted = false;
        }
        await _context.Set<Availability>().AddRangeAsync(availabilities);
        await SaveChangesHandlingDuplicateAsync();
    }

    public async Task ReplaceFutureUnbooked(
        Guid doctorId,
        DateOnly fromInclusive,
        DateOnly toExclusive,
        DateOnly currentDate,
        TimeOnly currentTime,
        IReadOnlyCollection<Availability> replacements)
    {
        EnsureWriteScope();
        // Releer dentro de la transacción obtenida por el servicio.
        var futureUnbooked = await _context.Set<Availability>()
            .Where(s => !s.Deleted && s.DoctorId == doctorId &&
                        s.Date >= fromInclusive && s.Date < toExclusive &&
                        s.Status != AvailabilityStatus.Booked &&
                        (s.Date > currentDate ||
                         (s.Date == currentDate && s.StartTime >= currentTime)))
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var slot in futureUnbooked)
        {
            slot.Deleted = true;
            slot.UpdatedAt = now;
        }
        // Liberar el índice único filtrado ANTES de insertar reemplazos.
        await SaveChangesHandlingDuplicateAsync();
        await AddRange(replacements);
    }

    private void EnsureWriteScope()
    {
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "Las escrituras de agenda requieren un alcance clínico activo.");
    }

    private async Task SaveChangesHandlingDuplicateAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException(
                "Otro cambio de agenda generó un horario o regla duplicado.",
                "AVAILABILITY_UNIQUE_CONFLICT");
        }
    }
}
