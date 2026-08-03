using System;
using System.Collections.Generic;
using System.Text;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dsw2026Tpi.Data.Configurations;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");

        builder.Property(patient => patient.UserId)
            .IsRequired();

        builder.Property(patient => patient.Dni)
            .IsRequired();

        builder.Property(patient => patient.FullName)
            .HasMaxLength(150)
            .IsRequired(false);

        builder.HasIndex(patient => patient.UserId)
            .IsUnique();

        builder.HasIndex(patient => patient.Dni)
            .IsUnique();
    }
}
