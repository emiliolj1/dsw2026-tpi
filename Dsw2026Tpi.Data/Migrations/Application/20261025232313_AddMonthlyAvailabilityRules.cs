using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dsw2026Tpi.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddMonthlyAvailabilityRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AvailabilityRuleId",
                table: "Availabilities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AvailabilityRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvailabilityRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvailabilityRules_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Availabilities_AvailabilityRuleId",
                table: "Availabilities",
                column: "AvailabilityRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityRules_DoctorId_Year_Month_DayOfWeek_StartTime_EndTime",
                table: "AvailabilityRules",
                columns: new[] { "DoctorId", "Year", "Month", "DayOfWeek", "StartTime", "EndTime" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Availabilities_AvailabilityRules_AvailabilityRuleId",
                table: "Availabilities",
                column: "AvailabilityRuleId",
                principalTable: "AvailabilityRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Availabilities_AvailabilityRules_AvailabilityRuleId",
                table: "Availabilities");

            migrationBuilder.DropTable(
                name: "AvailabilityRules");

            migrationBuilder.DropIndex(
                name: "IX_Availabilities_AvailabilityRuleId",
                table: "Availabilities");

            migrationBuilder.DropColumn(
                name: "AvailabilityRuleId",
                table: "Availabilities");
        }
    }
}
