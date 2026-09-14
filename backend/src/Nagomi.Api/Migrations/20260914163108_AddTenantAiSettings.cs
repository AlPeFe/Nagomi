using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nagomi.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantAiSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiBaseUrl",
                table: "tenant_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AiEnableTools",
                table: "tenant_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiEnabled",
                table: "tenant_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AiModel",
                table: "tenant_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiPassword",
                table: "tenant_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiProvider",
                table: "tenant_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiSystemPrompt",
                table: "tenant_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiUsername",
                table: "tenant_settings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiBaseUrl",
                table: "tenant_settings");

            migrationBuilder.DropColumn(
                name: "AiEnableTools",
                table: "tenant_settings");

            migrationBuilder.DropColumn(
                name: "AiEnabled",
                table: "tenant_settings");

            migrationBuilder.DropColumn(
                name: "AiModel",
                table: "tenant_settings");

            migrationBuilder.DropColumn(
                name: "AiPassword",
                table: "tenant_settings");

            migrationBuilder.DropColumn(
                name: "AiProvider",
                table: "tenant_settings");

            migrationBuilder.DropColumn(
                name: "AiSystemPrompt",
                table: "tenant_settings");

            migrationBuilder.DropColumn(
                name: "AiUsername",
                table: "tenant_settings");
        }
    }
}
