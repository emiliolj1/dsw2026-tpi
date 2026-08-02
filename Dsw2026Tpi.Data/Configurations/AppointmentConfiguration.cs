using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dsw2026Tpi.Data.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");

        builder.Property(appointment => appointment.DoctorId).IsRequired();

        builder.Property(appointment => appointment.AvailabilityId).IsRequired();

        builder.Property(appointment => appointment.PatientId).IsRequired();

        builder.Property(appointment => appointment.Reason).IsRequired();

        builder.Property(appointment => appointment.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(appointment => appointment.RowVersion).IsRowVersion();

        builder.HasOne(appointment => appointment.Doctor).WithMany().HasForeignKey(appointment => appointment.DoctorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(appointment => appointment.Availability).WithMany().HasForeignKey(appointment => appointment.AvailabilityId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(appointment => appointment.Patient).WithMany().HasForeignKey(appointment => appointment.PatientId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(appointment => appointment.PatientId);

        builder.HasIndex(appointment => appointment.DoctorId);

        builder.HasIndex(appointment => appointment.AvailabilityId).IsUnique().HasFilter("[Deleted] = 0 AND [Status] = 'BOOKED'");
    }
}
