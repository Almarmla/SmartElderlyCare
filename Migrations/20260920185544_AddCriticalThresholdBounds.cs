using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartElderlyCare.Migrations
{
    /// <inheritdoc />
    public partial class AddCriticalThresholdBounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CriticalHigh",
                table: "Thresholds",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CriticalLow",
                table: "Thresholds",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CriticalHigh",
                table: "Thresholds");

            migrationBuilder.DropColumn(
                name: "CriticalLow",
                table: "Thresholds");
        }
    }
}
