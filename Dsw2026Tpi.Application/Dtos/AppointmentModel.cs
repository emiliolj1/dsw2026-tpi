using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Dsw2026Tpi.Application.Dtos
{
    public record AppointmentModel
    {
        public record Request(
            Guid DoctorId,
            [property: JsonPropertyName("availabilitySlotId")]
            Guid AvailabilityId,
            PatientRequest Patient,
            string Reason);

        public record PatientRequest(long Dni);

        public record SearchRequest(
            Guid? SpecialityId,
            Guid? DoctorId,
            long? Dni,
            DateOnly? Date,
            int PageSize = 10,
            int PageIndex = 0,
            string? Status = null);

        public record Response(
            Guid AppointmentsId,
            string AppointmentsStatus,
            Guid AvailabilitySlotId,
            PatientResponse Patient,
            DoctorResponse Doctor,
            DateOnly Date,
            string StartTime,
            string EndTime,
            string AvailableTime,
            string Reason)
        {
            // TEMPORAL: solo para que AppointmentFlowIntegrationTests
            // (de Charly) siga compilando. No salen en el JSON.
            // Borrar cuando Charly actualice ese test en C04.
            [JsonIgnore] public Guid Id => AppointmentsId;
            [JsonIgnore] public Guid AvailabilityId => AvailabilitySlotId;
            [JsonIgnore] public string Status => AppointmentsStatus;
        }

        public record PagedResponse(
            IEnumerable<Response> Data,
            int Total,
            int PageSize,
            int PageIndex);

        public record PatientResponse(long Dni, string FullName);

        public record SpecialtyResponse(Guid SpecialtyId, string Name);

        public record DoctorResponse(
            Guid DoctorId,
            string Name,
            SpecialtyResponse Specialty);
    }
}