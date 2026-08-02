using Dsw2026Tpi.Domain.Entities;

namespace Dsw2026Tpi.Domain.Interfaces;

public interface IAppointmentPersistence
{
    Task<bool> TryCreate(Appointment appointment);
    Task<bool> TryCancel(Guid appointmentId);
}
