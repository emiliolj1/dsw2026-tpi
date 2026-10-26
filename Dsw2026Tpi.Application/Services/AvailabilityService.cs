using System.Diagnostics;
using System.Globalization;
using System.Text;
using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Application.Options;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dsw2026Tpi.Application.Services;

public class AvailabilityService : IAvailabilityService
{
    private const int SlotDurationMinutes = 30;
    private readonly IPersistence _persistence;
    private readonly IAvailabilityPersistence _availabilities;
    private readonly IClinicalWriteScopeFactory _writeScopes;
    private readonly IClinicClock _clock;
    private readonly ILogger<AvailabilityService> _logger;
    private readonly HashSet<DateOnly> _nonWorkingDays;

    public AvailabilityService(
        IPersistence persistence,
        IAvailabilityPersistence availabilities,
        IClinicalWriteScopeFactory writeScopes,
        IClinicClock clock,
        IOptions<NonWorkingDaysOptions> nonWorkingDaysOptions,
        ILogger<AvailabilityService> logger)
    {
        _persistence = persistence;
        _availabilities = availabilities;
        _writeScopes = writeScopes;
        _clock = clock;
        _logger = logger;
        _nonWorkingDays = nonWorkingDaysOptions.Value.Dates
            .Select(s => DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture))
            .ToHashSet();
    }

    public async Task<IEnumerable<AvailabilityModel.MonthlyPlanningResponse>> GetMonthlyPlanning(
        Guid doctorId)
    {
        ValidateDoctorId(doctorId);
        await EnsureDoctorExists(doctorId);
        var now = _clock.GetCurrentLocalDateTime();
        var rules = await _availabilities.GetRulesByDoctorAndMonth(doctorId, now.Year, now.Month);
        return MapRules(rules);
    }

    public async Task<IEnumerable<AvailabilityModel.AvailableSlotResponse>> GetAvailableSlots(
        Guid doctorId)
    {
        ValidateDoctorId(doctorId);
        await EnsureDoctorExists(doctorId);
        var now = _clock.GetCurrentLocalDateTime();
        var today = DateOnly.FromDateTime(now.DateTime);
        var time = TimeOnly.FromDateTime(now.DateTime);
        var slots = await _persistence.GetFiltered<Availability>(s =>
            s.DoctorId == doctorId && s.Status == AvailabilityStatus.Available &&
            (s.Date > today || (s.Date == today && s.StartTime >= time))) ?? [];
        return slots.OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .Select(s => new AvailabilityModel.AvailableSlotResponse(
                s.Id, s.DoctorId, s.Date, FormatTime(s.StartTime), FormatTime(s.EndTime)))
            .ToArray();
    }

    public async Task<AvailabilityModel.PlanningResponse> Create(AvailabilityModel.Request request)
    {
        var ranges = ValidateRequest(request, allowEmpty: false);
        var now = _clock.GetCurrentLocalDateTime(); // Un solo instante por operación.
        var today = DateOnly.FromDateTime(now.DateTime);
        var time = TimeOnly.FromDateTime(now.DateTime);
        var nextMonth = new DateOnly(now.Year, now.Month, 1).AddMonths(1);

        // G02: inicia transacción y toma sp_getapplock antes de lecturas decisorias.
        await using var scope = await _writeScopes.BeginAsync();
        await EnsureActiveDoctorExists(request.DoctorId);

        var existingRules = (await _availabilities.GetRulesByDoctorAndMonth(
            request.DoctorId, now.Year, now.Month)).ToList();
        foreach (var range in ranges)
        {
            if (existingRules.Any(r => r.DayOfWeek == range.Day &&
                Intersect(r.StartTime, r.EndTime, range.StartTime, range.EndTime) &&
                !(r.StartTime == range.StartTime && r.EndTime == range.EndTime)))
            {
                throw new ConflictException(
                    "El nuevo rango se superpone con una regla mensual existente.",
                    "AVAILABILITY_RULE_OVERLAP");
            }
        }

        var missingRules = ranges
            .Where(range => !existingRules.Any(r => Same(r, range)))
            .Select(range => NewRule(request.DoctorId, now.Year, now.Month, range))
            .ToList();
        await _availabilities.AddRules(missingRules);
        var finalRules = existingRules.Concat(missingRules).ToList();
        var candidates = GenerateSlots(request.DoctorId, finalRules, today, time, nextMonth);
        var existingSlots = await _availabilities.GetByDoctorAndPeriod(
            request.DoctorId, today, nextMonth);
        EnsureNoPartialOverlaps(candidates, existingSlots);
        var existingKeys = existingSlots.Select(s => (s.Date, s.StartTime)).ToHashSet();
        var newSlots = candidates.Where(s => !existingKeys.Contains((s.Date, s.StartTime))).ToList();
        await _availabilities.AddRange(newSlots);

        await scope.CompleteAsync();
        LogSaved("creada", request.DoctorId, now.Year, now.Month, finalRules.Count, newSlots.Count);
        return BuildResponse(request.DoctorId, now.Year, now.Month, finalRules);
    }

    public async Task<AvailabilityModel.PlanningResponse> Update(AvailabilityModel.Request request)
    {
        var ranges = ValidateRequest(request, allowEmpty: true);
        var now = _clock.GetCurrentLocalDateTime();
        var today = DateOnly.FromDateTime(now.DateTime);
        var time = TimeOnly.FromDateTime(now.DateTime);
        var nextMonth = new DateOnly(now.Year, now.Month, 1).AddMonths(1);

        await using var scope = await _writeScopes.BeginAsync();
        await EnsureActiveDoctorExists(request.DoctorId);
        var oldRules = (await _availabilities.GetRulesByDoctorAndMonth(
            request.DoctorId, now.Year, now.Month)).ToList();

        // Incluso PUT con days=[] y sin reglas previas debe retirar slots legado
        // futuros libres; por eso no se omite la regeneración.
        var obsoleteIds = oldRules.Where(r => !ranges.Any(range => Same(r, range)))
            .Select(r => r.Id).ToArray();
        var newRules = ranges.Where(range => !oldRules.Any(r => Same(r, range)))
            .Select(range => NewRule(request.DoctorId, now.Year, now.Month, range)).ToList();
        var remainingRules = oldRules.Where(r => !obsoleteIds.Contains(r.Id))
            .Concat(newRules).ToList();

        var candidates = GenerateSlots(request.DoctorId, remainingRules, today, time, nextMonth);
        var existingSlots = await _availabilities.GetByDoctorAndPeriod(request.DoctorId, today, nextMonth);
        var preserved = existingSlots.Where(s =>
            s.Status == AvailabilityStatus.Booked || IsPast(s, today, time)).ToList();
        EnsureNoPartialOverlaps(candidates, preserved);
        var reservedKeys = preserved.Where(s => s.Status == AvailabilityStatus.Booked)
            .Select(s => (s.Date, s.StartTime)).ToHashSet();
        var replacements = candidates.Where(s => !reservedKeys.Contains((s.Date, s.StartTime)))
            .ToList();

        // Orden de cambios seguro bajo el mismo alcance SQL y una sola confirmación final.
        await _availabilities.SoftDeleteRules(obsoleteIds);
        await _availabilities.AddRules(newRules);
        await _availabilities.ReplaceFutureUnbooked(
            request.DoctorId, today, nextMonth, today, time, replacements);
        await scope.CompleteAsync();

        LogSaved("actualizada", request.DoctorId, now.Year, now.Month, remainingRules.Count,
            replacements.Count);
        return BuildResponse(request.DoctorId, now.Year, now.Month, remainingRules);
    }

    private async Task EnsureDoctorExists(Guid doctorId)
    {
        if (await _persistence.GetById<Doctor>(doctorId) is null)
            throw new EntityNotFoundException(nameof(Doctor));
    }

    private async Task EnsureActiveDoctorExists(Guid doctorId)
    {
        var doctor = await _persistence.GetById<Doctor>(doctorId);
        if (doctor is null || !doctor.IsActive)
            throw new EntityNotFoundException(nameof(Doctor));
    }

    private void LogSaved(string action, Guid doctorId, int year, int month,
        int ruleCount, int slotCount)
    {
        _logger.LogInformation(
            "Agenda {Action}. DoctorId {DoctorId}; año {Year}; mes {Month}; " +
            "reglas {RuleCount}; slots escritos {SlotCount}; traceId {TraceId}",
            action, doctorId, year, month, ruleCount, slotCount,
            Activity.Current?.Id ?? "no-trace");
    }

    private static AvailabilityModel.PlanningResponse BuildResponse(
        Guid doctorId, int year, int month, IEnumerable<AvailabilityRule> rules) =>
        new(doctorId, year, month, MapRules(rules));

    private static AvailabilityModel.MonthlyPlanningResponse[] MapRules(
        IEnumerable<AvailabilityRule> rules) => rules
            .OrderBy(r => r.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)r.DayOfWeek)
            .ThenBy(r => r.StartTime)
            .Select(r => new AvailabilityModel.MonthlyPlanningResponse(
                r.Id, ToSpanishDay(r.DayOfWeek), FormatTime(r.StartTime), FormatTime(r.EndTime)))
            .ToArray();

    private static AvailabilityRule NewRule(Guid doctorId, int year, int month,
        ScheduleRange range) =>
        new(doctorId, year, month, range.Day, range.StartTime, range.EndTime);

    private List<Availability> GenerateSlots(Guid doctorId,
        IEnumerable<AvailabilityRule> rules, DateOnly today, TimeOnly now,
        DateOnly nextMonth)
    {
        var grouped = rules.GroupBy(r => r.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.ToList());
        var result = new List<Availability>();
        for (var date = today; date < nextMonth; date = date.AddDays(1))
        {
            if (_nonWorkingDays.Contains(date) || !grouped.TryGetValue(date.DayOfWeek, out var dayRules))
                continue;
            foreach (var rule in dayRules)
            {
                for (var time = rule.StartTime; time < rule.EndTime;
                     time = time.AddMinutes(SlotDurationMinutes))
                {
                    if (date == today && time < now) continue;
                    result.Add(new Availability(doctorId, date, time,
                        availabilityRuleId: rule.Id));
                }
            }
        }
        return result;
    }

    private static bool IsPast(Availability s, DateOnly today, TimeOnly time) =>
        s.Date < today || (s.Date == today && s.StartTime < time);

    private static void EnsureNoPartialOverlaps(
        IEnumerable<Availability> candidates, IEnumerable<Availability> existing)
    {
        var active = existing.ToList();
        foreach (var candidate in candidates)
        {
            if (active.Any(s => s.Date == candidate.Date &&
                Intersect(s.StartTime, s.EndTime, candidate.StartTime, candidate.EndTime) &&
                s.StartTime != candidate.StartTime))
            {
                throw new ConflictException(
                    "El horario se superpone con un slot existente que debe conservarse.",
                    "AVAILABILITY_SLOT_OVERLAP");
            }
        }
    }

    private static bool Intersect(TimeOnly aStart, TimeOnly aEnd,
        TimeOnly bStart, TimeOnly bEnd) => aStart < bEnd && bStart < aEnd;

    private static bool Same(AvailabilityRule rule, ScheduleRange range) =>
        rule.DayOfWeek == range.Day && rule.StartTime == range.StartTime &&
        rule.EndTime == range.EndTime;

    private static IReadOnlyList<ScheduleRange> ValidateRequest(
        AvailabilityModel.Request request, bool allowEmpty)
    {
        if (request is null)
            throw new ValidationException().WithDetail(nameof(request), "El cuerpo es obligatorio.");
        var errors = new List<(string Field, string Issue)>();
        var ranges = new List<ScheduleRange>();
        if (request.DoctorId == Guid.Empty)
            errors.Add((nameof(request.DoctorId), "Es obligatorio."));
        if (request.Days is null)
            errors.Add((nameof(request.Days), "Debe indicar days, aunque sea vacío en PUT."));
        else
        {
            var days = request.Days.ToList();
            if (days.Count == 0 && !allowEmpty)
                errors.Add((nameof(request.Days), "POST requiere al menos un día."));
            for (var i = 0; i < days.Count; i++)
            {
                var day = days[i];
                if (day is null)
                {
                    errors.Add(($"days[{i}]", "El rango es obligatorio."));
                    continue;
                }
                if (!TryParseDay(day.Day, out var weekDay))
                    errors.Add(($"days[{i}].day", "Día de semana inválido."));
                var validStart = TimeOnly.TryParseExact(day.StartTime?.Trim(),
                    "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start);
                var validEnd = TimeOnly.TryParseExact(day.EndTime?.Trim(),
                    "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end);
                if (!validStart) errors.Add(($"days[{i}].startTime", "Formato HH:mm obligatorio."));
                if (!validEnd) errors.Add(($"days[{i}].endTime", "Formato HH:mm obligatorio."));
                if (!TryParseDay(day.Day, out weekDay) || !validStart || !validEnd) continue;
                if (start >= end || (end - start).Ticks % TimeSpan.FromMinutes(30).Ticks != 0)
                {
                    errors.Add(($"days[{i}]", "Inicio anterior al fin y duración múltiplo de 30 minutos."));
                    continue;
                }
                ranges.Add(new ScheduleRange(weekDay, start, end));
            }
        }
        for (var i = 0; i < ranges.Count; i++)
        for (var j = i + 1; j < ranges.Count; j++)
        {
            if (ranges[i].Day == ranges[j].Day &&
                Intersect(ranges[i].StartTime, ranges[i].EndTime,
                    ranges[j].StartTime, ranges[j].EndTime))
                errors.Add((nameof(request.Days), "Existen rangos solapados o duplicados."));
        }
        if (errors.Count > 0) throw new ValidationException().WithDetail(errors);
        return ranges;
    }

    private static void ValidateDoctorId(Guid id)
    {
        if (id == Guid.Empty)
            throw new ValidationException().WithDetail("doctorId", "Es obligatorio.");
    }

    private static bool TryParseDay(string? input, out DayOfWeek day)
    {
        var text = RemoveDiacritics(input?.Trim().ToUpperInvariant() ?? "");
        day = text switch
        {
            "LUNES" or "MONDAY" => DayOfWeek.Monday,
            "MARTES" or "TUESDAY" => DayOfWeek.Tuesday,
            "MIERCOLES" or "WEDNESDAY" => DayOfWeek.Wednesday,
            "JUEVES" or "THURSDAY" => DayOfWeek.Thursday,
            "VIERNES" or "FRIDAY" => DayOfWeek.Friday,
            "SABADO" or "SATURDAY" => DayOfWeek.Saturday,
            "DOMINGO" or "SUNDAY" => DayOfWeek.Sunday,
            _ => (DayOfWeek)(-1)
        };
        return Enum.IsDefined(day);
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(c =>
            CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray()).Normalize(NormalizationForm.FormC);
    }

    private static string ToSpanishDay(DayOfWeek day) => day switch
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

    private static string FormatTime(TimeOnly time) =>
        time.ToString("HH:mm", CultureInfo.InvariantCulture);

    private sealed record ScheduleRange(DayOfWeek Day, TimeOnly StartTime, TimeOnly EndTime);
}
