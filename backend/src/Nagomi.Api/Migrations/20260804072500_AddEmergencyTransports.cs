using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagomi.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEmergencyTransports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "emergency_transports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Incident_Latitude = table.Column<decimal>(type: "numeric(9,6)", nullable: false),
                    Incident_Longitude = table.Column<decimal>(type: "numeric(9,6)", nullable: false),
                    Incident_Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Incident_Municipality = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Incident_Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emergency_transports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_emergency_transports_PublicId",
                table: "emergency_transports",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "emergency_transports");
        }
    }
}
