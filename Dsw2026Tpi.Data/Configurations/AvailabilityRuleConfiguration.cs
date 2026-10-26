using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dsw2026Tpi.Data.Configurations;

public class AvailabilityRuleConfiguration
    : IEntityTypeConfiguration<AvailabilityRule>
{
    public void Configure(
        EntityTypeBuilder<AvailabilityRule> builder)
    {
        builder.ToTable("AvailabilityRules");

        builder.HasKey(rule => rule.Id);

        builder.Property(rule => rule.DoctorId)
            .IsRequired();

        builder.Property(rule => rule.Year)
            .IsRequired();

        builder.Property(rule => rule.Month)
            .IsRequired();

        builder.Property(rule => rule.DayOfWeek)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(rule => rule.StartTime)
            .HasColumnType("time(0)")
            .IsRequired();

        builder.Property(rule => rule.EndTime)
            .HasColumnType("time(0)")
            .IsRequired();

        builder.HasOne<Doctor>()
            .WithMany()
            .HasForeignKey(rule => rule.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(rule => new
        {
            rule.DoctorId,
            rule.Year,
            rule.Month,
            rule.DayOfWeek,
            rule.StartTime,
            rule.EndTime
        })
        .IsUnique()
        .HasFilter("[Deleted] = 0");
    }
}