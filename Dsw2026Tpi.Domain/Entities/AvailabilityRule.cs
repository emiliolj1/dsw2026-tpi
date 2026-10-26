
namespace Dsw2026Tpi.Domain.Entities;

public class AvailabilityRule : EntityBase
{
    public Guid DoctorId { get; private set; }

    public int Year { get; private set; }

    public int Month { get; private set; }

    public DayOfWeek DayOfWeek { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    
    private AvailabilityRule() { }

    public AvailabilityRule(
        Guid doctorId,
        int year,
        int month,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime)
    {
        if (doctorId == Guid.Empty)
            throw new ArgumentException("DoctorId inválido.");

        if (year < 1 || year > 9999)
            throw new ArgumentOutOfRangeException(nameof(year));

        if (month < 1 || month > 12)
            throw new ArgumentOutOfRangeException(nameof(month));

        if (!Enum.IsDefined(dayOfWeek))
            throw new ArgumentOutOfRangeException(nameof(dayOfWeek));

        if (startTime >= endTime)
            throw new ArgumentException(
                "La hora de inicio debe ser anterior a la de fin.");

        if ((endTime - startTime).Ticks %
            TimeSpan.FromMinutes(30).Ticks != 0)
            throw new ArgumentException(
                "La duración debe ser múltiplo de 30 minutos.");

        DoctorId = doctorId;
        Year = year;
        Month = month;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }
}