using Dsw2026Tpi.Domain.Entities;

namespace Dsw2026Tpi.Domain.Interfaces
{
    public interface IAvailabilityPersistence
    {
        Task<IReadOnlyCollection<Availability>> GetByDoctorAndPeriod(Guid doctorId, DateOnly fromInclusive, DateOnly toExclusive);

        Task AddRange(IReadOnlyCollection<Availability> availabilities);

        Task ReplaceFutureUnbooked(
            Guid doctorId,
            DateOnly fromInclusive,
            DateOnly toExclusive,
            DateOnly currentDate,
            TimeOnly currentTime,
            IReadOnlyCollection<Availability> replacements
            );
    }
}
