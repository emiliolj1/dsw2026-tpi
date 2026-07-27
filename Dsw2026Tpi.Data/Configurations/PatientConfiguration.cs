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

        builder.Property(patient => patient.Email)
            .IsRequired()
            .HasMaxLength(254);

        builder.Property(patient => patient.NormalizedEmail)
            .IsRequired()
            .HasMaxLength(254);

        builder.Property(patient => patient.Dni)
            .IsRequired();

        builder.HasIndex(patient => patient.NormalizedEmail)
            .IsUnique();

        builder.HasIndex(patient => patient.Dni)
            .IsUnique();
    }
}