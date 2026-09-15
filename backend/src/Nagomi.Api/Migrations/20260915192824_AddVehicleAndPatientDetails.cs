using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagomi.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleAndPatientDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "vehicles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Plate",
                table: "vehicles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "patients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate",
                table: "patients",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "Plate",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "patients");
        }
    }
}
