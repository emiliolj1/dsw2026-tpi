using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;


namespace Dsw2026Tpi.Data
{
    public class AppointmentPersistenceEf : IAppointmentPersistence
    {
        private readonly Dsw2026TpiDbContext _context;

        public AppointmentPersistenceEf(Dsw2026TpiDbContext context)
        {
            _context = context;
        }

        public async Task<bool> TryCreate(Appointment appointment)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var availability = await _context.Set<Availability>().SingleOrDefaultAsync(current => current.Id == appointment.AvailabilityId && !current.Deleted);

                if (availability is null || availability.DoctorId != appointment.DoctorId || availability.Status != AvailabilityStatus.Available)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                var now = DateTime.UtcNow;

                availability.Book();
                availability.UpdatedAt = now;

                appointment.CreatedAt = now;
                appointment.UpdatedAt = now;
                appointment.Deleted = false;

                await _context.Set<Appointment>().AddAsync(appointment);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();

                return false;
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();

                return false;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> TryCancel(Guid appointmentId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var appointment = await _context.Set<Appointment>().SingleOrDefaultAsync(current => current.Id == appointmentId && !current.Deleted);

                if (appointment is null || appointment.Status != AppointmentStatus.BOOKED)
                {
                    await transaction.RollbackAsync();

                    return false;
                }

                var availability = await _context.Set<Availability>().SingleOrDefaultAsync(current => current.Id == appointment.AvailabilityId && !current.Deleted);

                if (availability is null || availability.Status != AvailabilityStatus.Booked)
                {
                    await transaction.RollbackAsync();

                    return false;
                }

                var now = DateTime.UtcNow;

                appointment.Cancel();
                appointment.UpdatedAt = now;

                availability.Release();
                availability.UpdatedAt = now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();

                return false;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
