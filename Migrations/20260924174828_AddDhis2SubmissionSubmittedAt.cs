using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartElderlyCare.Migrations
{
    /// <inheritdoc />
    public partial class AddDhis2SubmissionSubmittedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "Dhis2IndicatorSubmissions",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Dhis2IndicatorSubmissions");
        }
    }
}
