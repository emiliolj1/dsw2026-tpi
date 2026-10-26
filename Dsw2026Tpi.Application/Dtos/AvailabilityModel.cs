namespace Dsw2026Tpi.Application.Dtos;

public record AvailabilityModel
{
    // Contrato v1.7: POST/PUT reciben médico y rangos semanales.
    public record Request(Guid DoctorId, IEnumerable<DayRequest> Days);
    public record DayRequest(string Day, string StartTime, string EndTime);

    // GET /api/doctors/{id}/availabilities: configuración, no slots deducidos.
    public record MonthlyPlanningResponse(Guid Id, string Day, string StartTime, string EndTime);

    // POST 201 / PUT 200: planificación con identidad del mes.
    public record PlanningResponse(
        Guid DoctorId,
        int Year,
        int Month,
        IReadOnlyCollection<MonthlyPlanningResponse> Days);

    // GET /api/availabilities?doctorId=...
    public record AvailableSlotResponse(
        Guid AvailabilitySlotId,
        Guid DoctorId,
        DateOnly Date,
        string StartTime,
        string EndTime);
}
