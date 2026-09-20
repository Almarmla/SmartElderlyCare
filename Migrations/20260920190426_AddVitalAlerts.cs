using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartElderlyCare.Migrations
{
    /// <inheritdoc />
    public partial class AddVitalAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "Alerts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Metric",
                table: "Alerts",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Value",
                table: "Alerts",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VitalStatus",
                table: "Alerts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Warning");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "Metric",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "VitalStatus",
                table: "Alerts");
        }
    }
}
