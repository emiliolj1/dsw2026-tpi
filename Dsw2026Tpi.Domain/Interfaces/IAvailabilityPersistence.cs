using Dsw2026Tpi.Domain.Entities;

namespace Dsw2026Tpi.Domain.Interfaces;

public interface IAvailabilityPersistence
{
    Task<IReadOnlyCollection<Availability>> GetByDoctorAndPeriod(
        Guid doctorId, DateOnly fromInclusive, DateOnly toExclusive);

    Task<IReadOnlyCollection<AvailabilityRule>> GetRulesByDoctorAndMonth(
        Guid doctorId, int year, int month);

    // Los escritores se llaman dentro de IClinicalWriteScope, nunca con transacción propia.
    Task AddRules(IReadOnlyCollection<AvailabilityRule> rules);
    Task SoftDeleteRules(IReadOnlyCollection<Guid> ruleIds);
    Task AddRange(IReadOnlyCollection<Availability> availabilities);
    Task ReplaceFutureUnbooked(
        Guid doctorId,
        DateOnly fromInclusive,
        DateOnly toExclusive,
        DateOnly currentDate,
        TimeOnly currentTime,
        IReadOnlyCollection<Availability> replacements);
}
