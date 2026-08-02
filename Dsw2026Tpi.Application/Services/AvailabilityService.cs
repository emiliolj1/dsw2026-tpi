using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using System.Globalization;
using System.Text;
using Dsw2026Tpi.Application.Options;
using Microsoft.Extensions.Options;

namespace Dsw2026Tpi.Application.Services
{
    public class AvailabilityService : IAvailabilityService
    {
        private const int SlotDurationMinutes = 30;

        private readonly IPersistence _persistence;
        private readonly IAvailabilityPersistence _availabilityPersistence;
        private readonly HashSet<DateOnly> _nonWorkingDays;

        public AvailabilityService(
             IPersistence persistence,
             IAvailabilityPersistence availabilityPersistence,
             IOptions<NonWorkingDaysOptions> nonWorkingDaysOptions)
        {
            _persistence = persistence;
            _availabilityPersistence = availabilityPersistence;

            _nonWorkingDays = nonWorkingDaysOptions.Value.Dates
                .Select(date => DateOnly.ParseExact(
                    date,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture))
                .ToHashSet();
        }

        public async Task<IEnumerable<AvailabilityModel.AvailableSlotResponse>> GetAvailableSlots(Guid doctorId)
        {
            ValidateDoctorId(doctorId);
            await EnsureDoctorExists(doctorId);

            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);
            var currentTime = TimeOnly.FromDateTime(now);

            var availabilities =
                await _persistence.GetFiltered<Availability>(availability =>
                    availability.DoctorId == doctorId &&
                    availability.Status == AvailabilityStatus.Available &&
                    (availability.Date > today ||
                     availability.Date == today &&
                     availability.StartTime >= currentTime)) ?? [];

            return availabilities
                .OrderBy(availability => availability.Date)
                .ThenBy(availability => availability.StartTime)
                .Select(availability =>
                    new AvailabilityModel.AvailableSlotResponse(
                        availability.Id,
                        availability.DoctorId,
                        availability.Date,
                        availability.StartTime.ToString(
                            "HH:mm",
                            CultureInfo.InvariantCulture),
                        availability.EndTime.ToString(
                            "HH:mm",
                            CultureInfo.InvariantCulture)));
        }

        public async Task<IEnumerable<AvailabilityModel.MonthlyPlanningResponse>> GetMonthlyPlanning(Guid doctorId)
        {
            ValidateDoctorId(doctorId);
            await EnsureDoctorExists(doctorId);

            var now = DateTime.Now;
            var monthStart = new DateOnly(now.Year, now.Month, 1);
            var nextMonth = monthStart.AddMonths(1);

            var availabilities =
                await _availabilityPersistence.GetByDoctorAndPeriod(doctorId, monthStart, nextMonth);

            return BuildMonthlyPlanning(availabilities);
        }
        public async Task Create(AvailabilityModel.Request request)
        {
            var ranges = ValidateRequest(request);
            await EnsureDoctorExists(request.DoctorId);

            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);
            var currentTime = TimeOnly.FromDateTime(now);
            var nextMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(1);

            var candidates = GenerateSlots(
                request.DoctorId,
                ranges,
                today,
                currentTime,
                nextMonth);

            var existing =
                await _availabilityPersistence.GetByDoctorAndPeriod(request.DoctorId, today, nextMonth);

            EnsureNoPartialOverlaps(candidates, existing);

            var existingKeys = existing.Select(availability =>
                    (availability.Date, availability.StartTime))
                .ToHashSet();

            var missingSlots = candidates.Where(candidate =>
                    !existingKeys.Contains(
                        (candidate.Date, candidate.StartTime)))
                .ToList();

            await _availabilityPersistence.AddRange(missingSlots);
        }
        public async Task Update(AvailabilityModel.Request request)
        {
            var ranges = ValidateRequest(request);
            await EnsureDoctorExists(request.DoctorId);

            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);
            var currentTime = TimeOnly.FromDateTime(now);
            var nextMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(1);

            var candidates = GenerateSlots(
                request.DoctorId,
                ranges,
                today,
                currentTime,
                nextMonth);

            var existing =
                await _availabilityPersistence.GetByDoctorAndPeriod(request.DoctorId, today, nextMonth);

            var preserved = existing
                .Where(availability =>
                    availability.Status == AvailabilityStatus.Booked ||
                    availability.Date == today &&
                    availability.StartTime < currentTime)
                .ToList();

            EnsureNoPartialOverlaps(candidates, preserved);

            var bookedKeys = existing
                .Where(availability =>
                    availability.Status == AvailabilityStatus.Booked)
                .Select(availability =>
                    (availability.Date, availability.StartTime))
                .ToHashSet();

            var replacements = candidates
                .Where(candidate =>
                    !bookedKeys.Contains(
                        (candidate.Date, candidate.StartTime)))
                .ToList();

            await _availabilityPersistence.ReplaceFutureUnbooked(
                request.DoctorId,
                today,
                nextMonth,
                today,
                currentTime,
                replacements);
        }
        private async Task EnsureDoctorExists(Guid doctorId)
        {
            var doctor = await _persistence.GetById<Doctor>(doctorId);

            if (doctor is null)
                throw new EntityNotFoundException(nameof(Doctor));
        }
        private static IReadOnlyCollection<ScheduleRange> ValidateRequest(
        AvailabilityModel.Request request)
        {
            if (request is null)
                throw new ValidationException().WithDetail(nameof(request), "El cuerpo de la solicitud es obligatorio.");

            var errors = new List<(string Field, string Issue)>();
            var ranges = new List<ScheduleRange>();

            if (request.DoctorId == Guid.Empty)
                errors.Add((nameof(request.DoctorId), "Es obligatorio."));

            if (request.Days is null)
                errors.Add((nameof(request.Days), "Es obligatorio."));
            else
            {
                var days = request.Days.ToList();

                if (days.Count == 0)

                    errors.Add((nameof(request.Days), "Debe contener al menos un día."));


                for (var index = 0; index < days.Count; index++)
                {
                    ValidateDayRequest(days[index], index, ranges, errors);
                }
            }

            AddOverlapErrors(ranges, errors);
            ThrowIfInvalid(errors);

            return ranges;
        }

        private static void ValidateDoctorId(Guid doctorId)
        {
            if (doctorId == Guid.Empty)
                throw new ValidationException().WithDetail(nameof(doctorId), "Es obligatorio.");
        }

        private static void ValidateDayRequest(
            AvailabilityModel.DayRequest dayRequest,
            int index,
            ICollection<ScheduleRange> ranges,
            ICollection<(string Field, string Issue)> errors)
        {
            var dayField = $"days[{index}].day";
            var startField = $"days[{index}].startTime";
            var endField = $"days[{index}].endTime";

            if (dayRequest is null)
            {
                errors.Add(($"days[{index}]", "El día y sus horarios son obligatorios."));

                return;
            }

            var validDay = TryParseDay(dayRequest.Day, out var day);

            if (!validDay)
            {
                errors.Add((dayField, "Debe ser un día válido de la semana."));
            }

            var validStartTime = TryParseTime(dayRequest.StartTime, out var startTime);

            if (!validStartTime)
            {
                errors.Add((startField, "Debe tener el formato HH:mm."));
            }

            var validEndTime = TryParseTime(dayRequest.EndTime, out var endTime);

            if (!validEndTime)
            {
                errors.Add((endField, "Debe tener el formato HH:mm."));
            }

            if (!validDay || !validStartTime || !validEndTime)
                return;

            if (startTime >= endTime)
            {
                errors.Add(($"days[{index}]", "El horario de inicio debe ser anterior al horario de fin."));

                return;
            }

            var duration = endTime - startTime;

            if (duration.Ticks % TimeSpan.FromMinutes(SlotDurationMinutes).Ticks != 0)
            {
                errors.Add(($"days[{index}]", "El rango horario debe dividirse en intervalos completos de 30 minutos."));

                return;
            }

            ranges.Add(new ScheduleRange(day, startTime, endTime));
        }

        private static void AddOverlapErrors(IReadOnlyList<ScheduleRange> ranges, ICollection<(string Field, string Issue)> errors)
        {
            for (var firstIndex = 0; firstIndex < ranges.Count; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < ranges.Count; secondIndex++)
                {
                    var first = ranges[firstIndex];
                    var second = ranges[secondIndex];

                    if (first.Day == second.Day && first.StartTime < second.EndTime && second.StartTime < first.EndTime)
                    {
                        errors.Add((nameof(AvailabilityModel.Request.Days),
                            $"Existen horarios superpuestos para {ToSpanishDay(first.Day)}."));

                        return;
                    }
                }
            }
        }

        private List<Availability> GenerateSlots(
            Guid doctorId,
            IEnumerable<ScheduleRange> ranges,
            DateOnly today,
            TimeOnly currentTime,
            DateOnly nextMonth)
        {
            var rangesByDay = ranges
                .GroupBy(range => range.Day)
                .ToDictionary(group => group.Key, group => group.ToList());

            var slots = new List<Availability>();

            for (var date = today; date < nextMonth; date = date.AddDays(1))
            {
                if (_nonWorkingDays.Contains(date))
                    continue;

                if (!rangesByDay.TryGetValue(date.DayOfWeek, out var dayRanges))
                    continue;

                foreach (var range in dayRanges)
                {
                    for (var startTime = range.StartTime;
                         startTime < range.EndTime;
                         startTime = startTime.AddMinutes(SlotDurationMinutes))
                    {
                        if (date == today && startTime < currentTime)
                            continue;

                        slots.Add(new Availability(doctorId, date, startTime));
                    }
                }
            }

            return slots;
        }

        private static void EnsureNoPartialOverlaps(
            IEnumerable<Availability> candidates,
            IEnumerable<Availability> existing)
        {
            var existingList = existing.ToList();

            foreach (var candidate in candidates)
            {
                var overlapping = existingList.FirstOrDefault(current =>
                        current.Date == candidate.Date &&
                        current.StartTime < candidate.EndTime &&
                        candidate.StartTime < current.EndTime &&
                        current.StartTime != candidate.StartTime);

                if (overlapping is not null)
                {
                    throw new ValidationException().WithDetail(nameof(AvailabilityModel.Request.Days),
                        $"El horario se superpone con una disponibilidad existente del {candidate.Date:dd/MM/yyyy}.");
                }
            }
        }

        private static IEnumerable<AvailabilityModel.MonthlyPlanningResponse> BuildMonthlyPlanning(IEnumerable<Availability> availabilities)
        {
            var response = new List<AvailabilityModel.MonthlyPlanningResponse>();

            var groups = availabilities
                .GroupBy(availability =>
                    availability.Date.DayOfWeek)
                .OrderBy(group => DayOrder(group.Key));

            foreach (var group in groups)
            {
                var slots = group
                    .Select(availability => new TimeRange(
                        availability.StartTime,
                        availability.EndTime))
                    .Distinct()
                    .OrderBy(range => range.StartTime)
                    .ToList();

                if (slots.Count == 0)
                    continue;

                var currentStart = slots[0].StartTime;
                var currentEnd = slots[0].EndTime;

                foreach (var slot in slots.Skip(1))
                {
                    if (slot.StartTime <= currentEnd)
                    {
                        if (slot.EndTime > currentEnd)
                            currentEnd = slot.EndTime;

                        continue;
                    }
                    response.Add(ToMonthlyResponse(group.Key, currentStart, currentEnd));

                    currentStart = slot.StartTime;
                    currentEnd = slot.EndTime;
                }

                response.Add(ToMonthlyResponse(group.Key, currentStart, currentEnd));
            }

            return response;
        }

        private static AvailabilityModel.MonthlyPlanningResponse
            ToMonthlyResponse(DayOfWeek day, TimeOnly startTime, TimeOnly endTime)
        {
            return new AvailabilityModel.MonthlyPlanningResponse(ToSpanishDay(day),
                startTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                endTime.ToString( "HH:mm", CultureInfo.InvariantCulture));
        }

        private static bool TryParseTime(string? value, out TimeOnly time)
        {
            return TimeOnly.TryParseExact(value?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
        }

        private static bool TryParseDay(string? value, out DayOfWeek day)
        {
            var normalized = RemoveDiacritics(value?.Trim().ToUpperInvariant() ?? string.Empty);

            switch (normalized)
            {
                case "LUNES":
                case "MONDAY":
                    day = DayOfWeek.Monday;
                    return true;

                case "MARTES":
                case "TUESDAY":
                    day = DayOfWeek.Tuesday;
                    return true;

                case "MIERCOLES":
                case "WEDNESDAY":
                    day = DayOfWeek.Wednesday;
                    return true;

                case "JUEVES":
                case "THURSDAY":
                    day = DayOfWeek.Thursday;
                    return true;

                case "VIERNES":
                case "FRIDAY":
                    day = DayOfWeek.Friday;
                    return true;

                case "SABADO":
                case "SATURDAY":
                    day = DayOfWeek.Saturday;
                    return true;

                case "DOMINGO":
                case "SUNDAY":
                    day = DayOfWeek.Sunday;
                    return true;

                default:
                    day = default;
                    return false;
            }
        }

        private static string RemoveDiacritics(string value)
        {
            var normalized = value.Normalize(NormalizationForm.FormD);

            var characters = normalized
                .Where(character =>
                    CharUnicodeInfo.GetUnicodeCategory(character) !=
                    UnicodeCategory.NonSpacingMark)
                .ToArray();

            return new string(characters).Normalize(NormalizationForm.FormC);
        }

        private static int DayOrder(DayOfWeek day)
        {
            return day == DayOfWeek.Sunday? 7 : (int)day;
        }

        private static string ToSpanishDay(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => "LUNES",
                DayOfWeek.Tuesday => "MARTES",
                DayOfWeek.Wednesday => "MIÉRCOLES",
                DayOfWeek.Thursday => "JUEVES",
                DayOfWeek.Friday => "VIERNES",
                DayOfWeek.Saturday => "SÁBADO",
                DayOfWeek.Sunday => "DOMINGO",
                _ => throw new ArgumentOutOfRangeException(nameof(day))
            };
        }

        private static void ThrowIfInvalid(IEnumerable<(string Field, string Issue)> errors)
        {
            var errorList = errors.ToList();

            if (errorList.Count > 0)
                throw new ValidationException().WithDetail(errorList);
        }

        private sealed record ScheduleRange(DayOfWeek Day, TimeOnly StartTime, TimeOnly EndTime);

        private sealed record TimeRange(TimeOnly StartTime, TimeOnly EndTime);
    }

}
