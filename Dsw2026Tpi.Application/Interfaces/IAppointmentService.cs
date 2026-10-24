using Dsw2026Tpi.Application.Dtos;

namespace Dsw2026Tpi.Application.Interfaces;

public interface IAppointmentService
{
    Task<AppointmentModel.Response> Create(
        AppointmentModel.Request request,
        Guid patientUserId);

    Task<IEnumerable<AppointmentModel.Response>> GetActiveByPatient(
        long dni,
        Guid patientUserId);

    Task Cancel(
        Guid appointmentId,
        Guid patientUserId);

    Task<AppointmentModel.PagedResponse> GetByDate(
        DateOnly date,
        int pageSize = 10,
        int pageIndex = 0,
        string? status = null);

    Task<AppointmentModel.PagedResponse> Search(
        AppointmentModel.SearchRequest request);
}