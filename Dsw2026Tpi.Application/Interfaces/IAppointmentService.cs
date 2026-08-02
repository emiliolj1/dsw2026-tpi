using Dsw2026Tpi.Application.Dtos;

namespace Dsw2026Tpi.Application.Interfaces;

public interface IAppointmentService
{
    Task Create(AppointmentModel.Request request, string patientEmail);

    Task<IEnumerable<AppointmentModel.Response>> GetActiveByPatient(long dni, string patientEmail);

    Task Cancel(Guid appointmentId, string patientEmail);

    Task<IEnumerable<AppointmentModel.Response>> GetByDate(DateOnly date);

    Task<AppointmentModel.PagedResponse> Search(AppointmentModel.SearchRequest request);
}
