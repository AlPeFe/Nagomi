using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nagomi.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityIdentifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ActorIdentifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ActorDisplayName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    ProviderIdentifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProviderName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "healthcare_facilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Ccn = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Codcnh = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OfficialAddressText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Street = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Block = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Staircase = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Floor = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Door = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AdditionalDetails = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MunicipalityCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    ProvinceCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    AutonomousCommunityCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    SourceYear = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_healthcare_facilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ine_autonomous_communities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ine_autonomous_communities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ine_municipalities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ProvinceCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    AutonomousCommunityCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ine_municipalities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ine_provinces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AutonomousCommunityCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ine_provinces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "transport_reasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transport_reasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "transport_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Patient_FirstName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Patient_LastName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Patient_DocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Patient_HealthCardNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Patient_Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Reason_Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Reason_Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    DefaultOrigin_Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DefaultOrigin_FacilityPublicId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    DefaultOrigin_OfficialCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DefaultOrigin_Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    DefaultOrigin_Street = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    DefaultOrigin_Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DefaultOrigin_Block = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DefaultOrigin_Staircase = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DefaultOrigin_Floor = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DefaultOrigin_Door = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DefaultOrigin_AdditionalDetails = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DefaultOrigin_PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultOrigin_MunicipalityCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    DefaultOrigin_Municipality = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DefaultOrigin_ProvinceCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    DefaultOrigin_Province = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DefaultOrigin_Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DefaultOrigin_Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    DefaultOrigin_Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    DefaultOrigin_Observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DefaultDestination_Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DefaultDestination_FacilityPublicId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    DefaultDestination_OfficialCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DefaultDestination_Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    DefaultDestination_Street = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    DefaultDestination_Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DefaultDestination_Block = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DefaultDestination_Staircase = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DefaultDestination_Floor = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DefaultDestination_Door = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DefaultDestination_AdditionalDetails = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DefaultDestination_PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultDestination_MunicipalityCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    DefaultDestination_Municipality = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DefaultDestination_ProvinceCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    DefaultDestination_Province = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DefaultDestination_Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DefaultDestination_Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    DefaultDestination_Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    DefaultDestination_Observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Requirements_Mobility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Requirements_RequiresOxygen = table.Column<bool>(type: "boolean", nullable: false),
                    Requirements_OxygenConcentrationPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Requirements_OxygenFlowLitresPerMinute = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    Requirements_CompanionRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Requirements_MedicalStaffRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Requirements_IsolationRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Requirements_BariatricRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Requirements_StairsAssistanceRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ContractCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PrivateNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ProviderVisibleNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Recurrence_Id = table.Column<Guid>(type: "uuid", nullable: true),
                    Recurrence_StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Recurrence_EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Recurrence_UtcOffset = table.Column<TimeSpan>(type: "interval", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transport_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_changes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PreviousValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CurrentValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Protection = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_changes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_changes_audit_entries_AuditEntryId",
                        column: x => x.AuditEntryId,
                        principalTable: "audit_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "journeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransportRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Origin_Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Origin_FacilityPublicId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Origin_OfficialCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Origin_Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Origin_Street = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Origin_Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Origin_Block = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Origin_Staircase = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Origin_Floor = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Origin_Door = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Origin_AdditionalDetails = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Origin_PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Origin_MunicipalityCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    Origin_Municipality = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Origin_ProvinceCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Origin_Province = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Origin_Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Origin_Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Origin_Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Origin_Observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Destination_Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Destination_FacilityPublicId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Destination_OfficialCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Destination_Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Destination_Street = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Destination_Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Destination_Block = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Destination_Staircase = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Destination_Floor = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Destination_Door = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Destination_AdditionalDetails = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Destination_PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Destination_MunicipalityCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    Destination_Municipality = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Destination_ProvinceCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Destination_Province = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Destination_Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Destination_Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Destination_Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Destination_Observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Requirements_Mobility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Requirements_RequiresOxygen = table.Column<bool>(type: "boolean", nullable: false),
                    Requirements_OxygenConcentrationPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Requirements_OxygenFlowLitresPerMinute = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    Requirements_CompanionRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Requirements_MedicalStaffRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Requirements_IsolationRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Requirements_BariatricRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Requirements_StairsAssistanceRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Schedule_AppointmentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Schedule_ScheduledStartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Schedule_ScheduledPickupAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Schedule_PickupTimePending = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderVisibleNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ProviderReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsRecurrenceException = table.Column<bool>(type: "boolean", nullable: false),
                    IsManuallyAdded = table.Column<bool>(type: "boolean", nullable: false),
                    CurrentStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ActualActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActualArrivedAtOriginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActualPatientPickupAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActualArrivedAtDestinationAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActualCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrentCancellationReason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CurrentCancellingParty = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_journeys_transport_requests_TransportRequestId",
                        column: x => x.TransportRequestId,
                        principalTable: "transport_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recurrence_weekday_schedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DayOfWeek = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    OutboundAppointmentTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    OutboundStartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    OutboundPickupTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ReturnPickupTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ReturnPickupNextDay = table.Column<bool>(type: "boolean", nullable: false),
                    ReturnPickupTimePending = table.Column<bool>(type: "boolean", nullable: false),
                    RecurrencePatternTransportRequestId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurrence_weekday_schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recurrence_weekday_schedules_transport_requests_RecurrenceP~",
                        column: x => x.RecurrencePatternTransportRequestId,
                        principalTable: "transport_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "journey_status_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JourneyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExternalResourceCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CancellingParty = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journey_status_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_journey_status_events_journeys_JourneyId",
                        column: x => x.JourneyId,
                        principalTable: "journeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_changes_AuditEntryId",
                table: "audit_changes",
                column: "AuditEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_EntityType_EntityIdentifier_ReceivedAt",
                table: "audit_entries",
                columns: new[] { "EntityType", "EntityIdentifier", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_healthcare_facilities_Ccn",
                table: "healthcare_facilities",
                column: "Ccn",
                unique: true,
                filter: "\"Ccn\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_healthcare_facilities_Codcnh",
                table: "healthcare_facilities",
                column: "Codcnh",
                unique: true,
                filter: "\"Codcnh\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_healthcare_facilities_Name_MunicipalityCode",
                table: "healthcare_facilities",
                columns: new[] { "Name", "MunicipalityCode" });

            migrationBuilder.CreateIndex(
                name: "IX_healthcare_facilities_PublicId",
                table: "healthcare_facilities",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ine_autonomous_communities_Code",
                table: "ine_autonomous_communities",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ine_municipalities_Code",
                table: "ine_municipalities",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ine_municipalities_ProvinceCode",
                table: "ine_municipalities",
                column: "ProvinceCode");

            migrationBuilder.CreateIndex(
                name: "IX_ine_provinces_AutonomousCommunityCode",
                table: "ine_provinces",
                column: "AutonomousCommunityCode");

            migrationBuilder.CreateIndex(
                name: "IX_ine_provinces_Code",
                table: "ine_provinces",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journey_status_events_JourneyId_IdempotencyKey",
                table: "journey_status_events",
                columns: new[] { "JourneyId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journey_status_events_JourneyId_OccurredAt",
                table: "journey_status_events",
                columns: new[] { "JourneyId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_journeys_PublicId",
                table: "journeys",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journeys_TransportRequestId_ServiceDate_Direction",
                table: "journeys",
                columns: new[] { "TransportRequestId", "ServiceDate", "Direction" });

            migrationBuilder.CreateIndex(
                name: "IX_recurrence_weekday_schedules_RecurrencePatternTransportRequ~",
                table: "recurrence_weekday_schedules",
                column: "RecurrencePatternTransportRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_transport_reasons_Code",
                table: "transport_reasons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transport_requests_PublicId",
                table: "transport_requests",
                column: "PublicId",
                unique: true,
                filter: "\"PublicId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_changes");

            migrationBuilder.DropTable(
                name: "healthcare_facilities");

            migrationBuilder.DropTable(
                name: "ine_autonomous_communities");

            migrationBuilder.DropTable(
                name: "ine_municipalities");

            migrationBuilder.DropTable(
                name: "ine_provinces");

            migrationBuilder.DropTable(
                name: "journey_status_events");

            migrationBuilder.DropTable(
                name: "recurrence_weekday_schedules");

            migrationBuilder.DropTable(
                name: "transport_reasons");

            migrationBuilder.DropTable(
                name: "audit_entries");

            migrationBuilder.DropTable(
                name: "journeys");

            migrationBuilder.DropTable(
                name: "transport_requests");
        }
    }
}
