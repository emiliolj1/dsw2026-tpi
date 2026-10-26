using Dsw2026Tpi.Application.Dtos;

namespace Dsw2026Tpi.Application.Interfaces;

public interface IAvailabilityService
{
    Task<IEnumerable<AvailabilityModel.MonthlyPlanningResponse>> GetMonthlyPlanning(Guid doctorId);
    Task<AvailabilityModel.PlanningResponse> Create(AvailabilityModel.Request request);
    Task<AvailabilityModel.PlanningResponse> Update(AvailabilityModel.Request request);
    Task<IEnumerable<AvailabilityModel.AvailableSlotResponse>> GetAvailableSlots(Guid doctorId);
}
