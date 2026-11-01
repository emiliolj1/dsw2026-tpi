using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Dsw2026Tpi.Data.Repositories
{
    // La transacción la maneja el alcance de escritura (IClinicalWriteScope)
    // que abre el servicio. Este repositorio solo modifica y guarda.
    public class AppointmentPersistenceEf : IAppointmentPersistence
    {
        private readonly Dsw2026TpiDbContext _context;

        public AppointmentPersistenceEf(Dsw2026TpiDbContext context)
        {
            _context = context;
        }

        public async Task<bool> TryCreate(Appointment appointment)
        {
            var availability = await _context.Set<Availability>()
                .SingleOrDefaultAsync(current =>
                    current.Id == appointment.AvailabilityId && !current.Deleted);

            if (availability is null
                || availability.DoctorId != appointment.DoctorId
                || availability.Status != AvailabilityStatus.Available)
            {
                return false;
            }

            var now = DateTime.UtcNow;

            availability.Book();
            availability.UpdatedAt = now;

            appointment.CreatedAt = now;
            appointment.UpdatedAt = now;
            appointment.Deleted = false;

            await _context.Set<Appointment>().AddAsync(appointment);

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                _context.ChangeTracker.Clear();
                return false;
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                // Doble reserva detectada por el índice único: conflicto esperado.
                _context.ChangeTracker.Clear();
                return false;
            }
            // Cualquier otro error de base se propaga (termina en HTTP 500).
        }

        public async Task<bool> TryCancel(Guid appointmentId)
        {
            var appointment = await _context.Set<Appointment>()
                .SingleOrDefaultAsync(current =>
                    current.Id == appointmentId && !current.Deleted);

            if (appointment is null || appointment.Status != AppointmentStatus.BOOKED)
            {
                return false;
            }

            var availability = await _context.Set<Availability>()
                .SingleOrDefaultAsync(current =>
                    current.Id == appointment.AvailabilityId && !current.Deleted);

            if (availability is null || availability.Status != AvailabilityStatus.Booked)
            {
                return false;
            }

            var now = DateTime.UtcNow;

            appointment.Cancel();
            appointment.UpdatedAt = now;

            availability.Release();
            availability.UpdatedAt = now;

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                _context.ChangeTracker.Clear();
                return false;
            }
        }

        private static bool IsUniqueViolation(DbUpdateException exception)
        {
            // 2601 y 2627: violación de índice único / restricción única en SQL Server.
            return exception.InnerException is SqlException { Number: 2601 or 2627 };
        }
    }
}