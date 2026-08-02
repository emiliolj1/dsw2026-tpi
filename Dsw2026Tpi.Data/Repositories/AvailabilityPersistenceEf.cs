using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Dsw2026Tpi.Data.Repositories
{
    public class AvailabilityPersistenceEf : IAvailabilityPersistence
    {
        private readonly Dsw2026TpiDbContext _context;

        public AvailabilityPersistenceEf(Dsw2026TpiDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyCollection<Availability>> GetByDoctorAndPeriod(Guid doctorId, DateOnly fromInclusive, DateOnly toExclusive)
        {
            return await _context.Set<Availability>()
                .AsNoTracking()
                .Where(availability =>
                !availability.Deleted &&
                availability.DoctorId == doctorId &&
                availability.Date >= fromInclusive &&
                availability.Date < toExclusive)
            .OrderBy(availability => availability.Date)
            .ThenBy(availability => availability.StartTime)
            .ToListAsync();
        }

        public async Task AddRange(IReadOnlyCollection<Availability> availabilities)
        {
            if (availabilities.Count == 0)
                return;

            var now = DateTime.UtcNow;

            foreach (var availability in availabilities)
            {
                availability.CreatedAt = now;
                availability.UpdatedAt = now;
                availability.Deleted = false;
            }
            await _context.Set<Availability>().AddRangeAsync(availabilities);
            await _context.SaveChangesAsync();
        }
        public async Task ReplaceFutureUnbooked(
        Guid doctorId,
        DateOnly fromInclusive,
        DateOnly toExclusive,
        DateOnly currentDate,
        TimeOnly currentTime,
        IReadOnlyCollection<Availability> replacements)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var futureUnbooked = await _context.Set<Availability>()
                    .Where(availability =>
                        !availability.Deleted &&
                        availability.DoctorId == doctorId &&
                        availability.Date >= fromInclusive &&
                        availability.Date < toExclusive &&
                        availability.Status != AvailabilityStatus.Booked &&
                        (availability.Date > currentDate ||
                         availability.Date == currentDate &&
                         availability.StartTime >= currentTime))
                    .ToListAsync();

                var now = DateTime.UtcNow;

                foreach (var availability in futureUnbooked)
                {
                    availability.Deleted = true;
                    availability.UpdatedAt = now;
                }

                await _context.SaveChangesAsync();

                if (replacements.Count > 0)
                {
                    foreach (var replacement in replacements)
                    {
                        replacement.CreatedAt = now;
                        replacement.UpdatedAt = now;
                        replacement.Deleted = false;
                    }

                    await _context.Set<Availability>().AddRangeAsync(replacements);

                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
