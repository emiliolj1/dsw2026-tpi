using Dsw2026Tpi.Application.Dtos;

namespace Dsw2026Tpi.Application.Interfaces
{
    public interface IAvailabilityService
    {
        Task<IEnumerable<AvailabilityModel.MonthlyPlanningResponse>> GetMonthlyPlanning(Guid doctorId);

        Task Create(AvailabilityModel.Request request);

        Task Update(AvailabilityModel.Request request);

        Task<IEnumerable<AvailabilityModel.AvailableSlotResponse>> GetAvailableSlots(Guid doctorId);
    }
}
