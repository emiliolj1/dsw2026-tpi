using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Application.Dtos
{
    public record AppointmentModel
    {
        public record Request(Guid DoctorId, Guid AvailabilityId, PatientRequest Patient, string Reason);

        public record PatientRequest(long Dni);

        public record SearchRequest(Guid? SpecialityId, Guid? DoctorId, long? Dni, DateOnly? Date, int PageSize = 10, int PageIndex = 0);

        public record Response(Guid Id, Guid AvailabilityId, SpecialtyResponse Specialty, DoctorResponse Doctor, long Dni, DateOnly Date, string AvailableTime, string Reason, string Status);

        public record PagedResponse(IEnumerable<Response> Data, int Total, int PageSize, int PageIndex);

        public record SpecialtyResponse(Guid Id, string Name);

        public record DoctorResponse(Guid Id, string Name);
    }
}
