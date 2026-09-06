using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagomi.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleTypeAndDriver : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VehicleType",
                table: "vehicles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DriverName",
                table: "journeys",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VehicleType",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "DriverName",
                table: "journeys");
        }
    }
}
