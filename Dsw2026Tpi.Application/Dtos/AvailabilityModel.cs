namespace Dsw2026Tpi.Application.Dtos
{
    public record AvailabilityModel
    {
        public record Request(
            Guid DoctorId,
            IEnumerable<DayRequest> Days
            );
        public record DayRequest(
            string Day,
            string StartTime,
            string EndTime
            );
        public record MonthlyPlanningResponse(
            string Day,
            string StartTime,
            string EndTime
            );
        public record AvailableSlotResponse(
            Guid AvailabilityId,
            Guid DoctorId,
            DateOnly Date,
            string StartTime,
            string EndTime
            );
    }
}
