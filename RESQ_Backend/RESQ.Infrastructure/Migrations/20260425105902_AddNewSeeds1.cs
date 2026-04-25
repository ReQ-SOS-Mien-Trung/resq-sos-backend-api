using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RESQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewSeeds1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "generated_sos_request_id",
                table: "mission_teams",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "safety_latest_checkin_at",
                table: "mission_teams",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "safety_status",
                table: "mission_teams",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "safety_timeout_at",
                table: "mission_teams",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_mission_teams_safety_status_timeout",
                table: "mission_teams",
                columns: new[] { "safety_status", "safety_timeout_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_mission_teams_safety_status_timeout",
                table: "mission_teams");

            migrationBuilder.DropColumn(
                name: "generated_sos_request_id",
                table: "mission_teams");

            migrationBuilder.DropColumn(
                name: "safety_latest_checkin_at",
                table: "mission_teams");

            migrationBuilder.DropColumn(
                name: "safety_status",
                table: "mission_teams");

            migrationBuilder.DropColumn(
                name: "safety_timeout_at",
                table: "mission_teams");
        }
    }
}
