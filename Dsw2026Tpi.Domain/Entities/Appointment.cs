using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities
{
    public class Appointment  : EntityBase
    {
        public Guid DoctorId { get; private set; }
        public Guid AvailabilityId { get; private set; }
        public Guid PatientId { get; private set; }
        public string Reason { get; private set; }
        public AppointmentStatus Status { get; private set; }
        public byte[] RowVersion { get; private set; } = [];

        public Doctor? Doctor { get; private set; }
        public Availability? Availability { get; private set; }
        public Patient? Patient { get; private set; }
        #region Constructor for EF
#pragma warning disable CS8618
        private Appointment()
        {
        }
    #pragma warning restore CS8618
        #endregion

        public Appointment(Guid doctorId,Guid availabilityId, Guid patientId, string reason, Guid? id = null) : base(id)
        {
            if (doctorId == Guid.Empty)
                throw new ArgumentException("El ID del Doctor es obligatorio.", nameof(doctorId));

            if (availabilityId == Guid.Empty)
                throw new ArgumentException("El ID de la Disponibilidad es obligatorio.", nameof(availabilityId));

            if (patientId == Guid.Empty)
                throw new ArgumentException("El ID del Paciente es obligatorio.", nameof(patientId));

            ArgumentException.ThrowIfNullOrWhiteSpace(reason);

            if (reason.Trim().Length < 5)
                throw new ArgumentException("El motivo debe contener al menos 5 caracteres.", nameof(reason));

            DoctorId = doctorId;
            AvailabilityId = availabilityId;
            PatientId = patientId;
            Reason = reason.Trim();
            Status = AppointmentStatus.BOOKED;
        }

        public void Cancel()
        {
            if (Status != AppointmentStatus.BOOKED)
                throw new InvalidOperationException("Solo se puede cancelar una cita reservada.");

            Status = AppointmentStatus.CANCELLED;
        }

        public void MarkAsAttended()
        {
            if (Status != AppointmentStatus.BOOKED)
                throw new InvalidOperationException("Solo una cita reservada puede marcarse como atendida");

            Status = AppointmentStatus.ATTENDED;
        }

        public void MarkAsNoShow()
        {
            if (Status != AppointmentStatus.BOOKED)
                throw new InvalidOperationException("Solo una cita reservada puede marcarse como ausente.");

            Status = AppointmentStatus.NO_SHOW;
        }

    }
}
