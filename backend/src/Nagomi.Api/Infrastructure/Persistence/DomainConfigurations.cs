using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.TransportRequests;

namespace Nagomi.Api.Infrastructure.Persistence;

internal sealed class TransportRequestRecordConfiguration : IEntityTypeConfiguration<TransportRequestRecord>
{
    public void Configure(EntityTypeBuilder<TransportRequestRecord> entity)
    {
        entity.ToTable("transport_requests");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.PublicId).IsUnique().HasFilter("\"PublicId\" IS NOT NULL");
        entity.Property(x => x.PublicId).HasMaxLength(40);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.ContractCode).HasMaxLength(100);
        entity.Property(x => x.ProviderName).HasMaxLength(200);
        entity.Property(x => x.ProviderReference).HasMaxLength(200);
        entity.Property(x => x.PrivateNotes).HasMaxLength(4000);
        entity.Property(x => x.ProviderVisibleNotes).HasMaxLength(4000);

        entity.OwnsOne(x => x.Patient, owned =>
        {
            owned.Property(x => x.FirstName).HasMaxLength(150);
            owned.Property(x => x.LastName).HasMaxLength(250);
            owned.Property(x => x.DocumentNumber).HasMaxLength(100);
            owned.Property(x => x.HealthCardNumber).HasMaxLength(100);
            owned.Property(x => x.Phone).HasMaxLength(50);
        });
        entity.OwnsOne(x => x.Reason, owned =>
        {
            owned.Property(x => x.Code).HasMaxLength(50).IsRequired();
            owned.Property(x => x.Description).HasMaxLength(250).IsRequired();
        });
        entity.OwnsOne(x => x.DefaultOrigin, ConfigureLocation);
        entity.OwnsOne(x => x.DefaultDestination, ConfigureLocation);
        entity.OwnsOne(x => x.Requirements, ConfigureRequirements);
        entity.OwnsOne(x => x.Recurrence, recurrence =>
        {
            recurrence.Property(x => x.StartDate).HasColumnType("date");
            recurrence.Property(x => x.EndDate).HasColumnType("date");
            recurrence.OwnsMany(x => x.WeekdaySchedules, schedule =>
            {
                schedule.ToTable("recurrence_weekday_schedules");
                schedule.Property<int>("Id");
                schedule.HasKey("Id");
                schedule.Property(x => x.DayOfWeek).HasConversion<string>().HasMaxLength(10);
            });
        });

        entity.HasMany(x => x.JourneyRecords)
            .WithOne()
            .HasForeignKey(x => x.TransportRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    internal static void ConfigureLocation<TOwner>(OwnedNavigationBuilder<TOwner, LocationSnapshot> owned)
        where TOwner : class
    {
        owned.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        owned.Property(x => x.FacilityPublicId).HasMaxLength(40);
        owned.Property(x => x.OfficialCode).HasMaxLength(50);
        owned.Property(x => x.Name).HasMaxLength(250);
        owned.Property(x => x.Street).HasMaxLength(250);
        owned.Property(x => x.Number).HasMaxLength(30);
        owned.Property(x => x.Block).HasMaxLength(30);
        owned.Property(x => x.Staircase).HasMaxLength(30);
        owned.Property(x => x.Floor).HasMaxLength(30);
        owned.Property(x => x.Door).HasMaxLength(30);
        owned.Property(x => x.AdditionalDetails).HasMaxLength(500);
        owned.Property(x => x.PostalCode).HasMaxLength(20);
        owned.Property(x => x.MunicipalityCode).HasMaxLength(5);
        owned.Property(x => x.Municipality).HasMaxLength(150);
        owned.Property(x => x.ProvinceCode).HasMaxLength(2);
        owned.Property(x => x.Province).HasMaxLength(150);
        owned.Property(x => x.Phone).HasMaxLength(50);
        owned.Property(x => x.Latitude).HasPrecision(9, 6);
        owned.Property(x => x.Longitude).HasPrecision(9, 6);
        owned.Property(x => x.Observations).HasMaxLength(2000);
    }

    internal static void ConfigureRequirements<TOwner>(OwnedNavigationBuilder<TOwner, TransportRequirements> owned)
        where TOwner : class
    {
        owned.Property(x => x.Mobility).HasConversion<string>().HasMaxLength(20);
        owned.Property(x => x.OxygenConcentrationPercent).HasPrecision(5, 2);
        owned.Property(x => x.OxygenFlowLitresPerMinute).HasPrecision(6, 2);
    }
}

internal sealed class JourneyRecordConfiguration : IEntityTypeConfiguration<JourneyRecord>
{
    public void Configure(EntityTypeBuilder<JourneyRecord> entity)
    {
        entity.ToTable("journeys");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.PublicId).IsUnique();
        entity.HasIndex(x => new { x.TransportRequestId, x.ServiceDate, x.Direction });
        entity.Property(x => x.PublicId).HasMaxLength(40).IsRequired();
        entity.Property(x => x.Direction).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.ServiceDate).HasColumnType("date");
        entity.Property(x => x.ProviderVisibleNotes).HasMaxLength(4000);
        entity.Property(x => x.ProviderReference).HasMaxLength(200);
        entity.Property(x => x.CurrentStatus).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.CurrentCancellationReason).HasConversion<string>().HasMaxLength(40);
        entity.Property(x => x.CurrentCancellingParty).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.RetrievalState).HasMaxLength(50);
        entity.OwnsOne(x => x.Origin, TransportRequestRecordConfiguration.ConfigureLocation);
        entity.OwnsOne(x => x.Destination, TransportRequestRecordConfiguration.ConfigureLocation);
        entity.OwnsOne(x => x.Requirements, TransportRequestRecordConfiguration.ConfigureRequirements);
        entity.OwnsOne(x => x.Schedule);
        entity.HasMany(x => x.StatusHistory)
            .WithOne()
            .HasForeignKey(x => x.JourneyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class JourneyStatusRecordConfiguration : IEntityTypeConfiguration<JourneyStatusRecord>
{
    public void Configure(EntityTypeBuilder<JourneyStatusRecord> entity)
    {
        entity.ToTable("journey_status_events");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => new { x.JourneyId, x.IdempotencyKey }).IsUnique();
        entity.HasIndex(x => new { x.JourneyId, x.OccurredAt });
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.Actor).HasMaxLength(200).IsRequired();
        entity.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        entity.Property(x => x.ExternalResourceCode).HasMaxLength(200);
        entity.Property(x => x.Latitude).HasPrecision(9, 6);
        entity.Property(x => x.Longitude).HasPrecision(9, 6);
        entity.Property(x => x.CancellationReason).HasConversion<string>().HasMaxLength(40);
        entity.Property(x => x.CancellingParty).HasConversion<string>().HasMaxLength(30);
    }
}

internal sealed class TransportAuditRecordConfiguration : IEntityTypeConfiguration<TransportAuditRecord>
{
    public void Configure(EntityTypeBuilder<TransportAuditRecord> entity)
    {
        entity.ToTable("transport_audit");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => new { x.EntityType, x.EntityIdentifier, x.RecordedAt });
        entity.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        entity.Property(x => x.EntityIdentifier).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Action).HasMaxLength(30).IsRequired();
        entity.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.Actor).HasMaxLength(200).IsRequired();
    }
}
