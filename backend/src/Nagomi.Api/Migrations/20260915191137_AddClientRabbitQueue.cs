using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagomi.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddClientRabbitQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RabbitQueue",
                table: "transport_clients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetQueue",
                table: "provider_notifications",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RabbitQueue",
                table: "transport_clients");

            migrationBuilder.DropColumn(
                name: "TargetQueue",
                table: "provider_notifications");
        }
    }
}
