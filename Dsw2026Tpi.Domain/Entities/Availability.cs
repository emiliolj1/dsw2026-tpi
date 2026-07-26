using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities
{
    public class Availability : EntityBase
    {
        public Guid DoctorId { get; private set; }
        public DateOnly Date { get; private set; }
        public TimeOnly StartTime { get; private set; }
        public TimeOnly EndTime { get; private set; }
        public AvailabilityStatus Status { get; private set; }
        public byte[] RowVersion { get; private set; } = [];

        #region Constructor for EF
    #pragma warning disable CS8618
        private Availability()
        {
        }
    #pragma warning restore CS8618
        #endregion
        public Availability(Guid doctorId, DateOnly date, TimeOnly startTime, Guid? id = null) : base(id)
        {
            if (DoctorId == Guid.Empty)
                throw new ArgumentException("DoctorId cannot be empty.", nameof(doctorId));

            DoctorId = doctorId;
            Date = date;
            StartTime = startTime;
            EndTime = startTime.AddMinutes(30);
            Status = AvailabilityStatus.Available;
        }

        public void Book()
        {
            if (Status != AvailabilityStatus.Available)
            {
                throw new InvalidOperationException("Solo se puede reservar un espacio Disponible.");
            }

            Status = AvailabilityStatus.Booked;
        }
        public void Release()
        {
            if (Status != AvailabilityStatus.Booked)
            {
                throw new InvalidOperationException("Solo se puede liberar un espacio Reservado");
            }

            Status = AvailabilityStatus.Available;
        }
        public void Block()
        {
            if (Status == AvailabilityStatus.Booked)
            {
                throw new InvalidOperationException("Un espacio reservado no puede ser Bloqueado");
            }

            Status = AvailabilityStatus.Blocked;
        }
    }
}
