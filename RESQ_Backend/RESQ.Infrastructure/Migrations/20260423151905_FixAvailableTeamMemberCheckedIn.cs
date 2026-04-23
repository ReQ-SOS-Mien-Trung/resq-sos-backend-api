using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RESQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixAvailableTeamMemberCheckedIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "confidence_score",
                table: "rescue_team_ai_suggestions");

            migrationBuilder.DropColumn(
                name: "confidence_score",
                table: "mission_ai_suggestions");

            migrationBuilder.DropColumn(
                name: "confidence_score",
                table: "cluster_ai_analysis");

            migrationBuilder.DropColumn(
                name: "confidence_score",
                table: "activity_ai_suggestions");

            migrationBuilder.RenameColumn(
                name: "confidence_score",
                table: "sos_ai_analysis",
                newName: "suggested_priority_score");

            migrationBuilder.AddColumn<bool>(
                name: "agrees_with_rule_base",
                table: "sos_ai_analysis",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suggested_mission_type",
                table: "mission_ai_suggestions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suggested_severity_level",
                table: "mission_ai_suggestions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "assembly_point_check_in_radius_configs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    assembly_point_id = table.Column<int>(type: "integer", nullable: false),
                    max_radius_meters = table.Column<double>(type: "double precision", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("assembly_point_check_in_radius_configs_pkey", x => x.id);
                    table.CheckConstraint("CK_assembly_point_check_in_radius_configs_max_radius_meters_po~", "\"max_radius_meters\" > 0");
                    table.ForeignKey(
                        name: "FK_assembly_point_check_in_radius_configs_assembly_point",
                        column: x => x.assembly_point_id,
                        principalTable: "assembly_points",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assembly_point_check_in_radius_configs_assembly_point_id",
                table: "assembly_point_check_in_radius_configs",
                column: "assembly_point_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assembly_point_check_in_radius_configs");

            migrationBuilder.DropColumn(
                name: "agrees_with_rule_base",
                table: "sos_ai_analysis");

            migrationBuilder.DropColumn(
                name: "suggested_mission_type",
                table: "mission_ai_suggestions");

            migrationBuilder.DropColumn(
                name: "suggested_severity_level",
                table: "mission_ai_suggestions");

            migrationBuilder.RenameColumn(
                name: "suggested_priority_score",
                table: "sos_ai_analysis",
                newName: "confidence_score");

            migrationBuilder.AddColumn<double>(
                name: "confidence_score",
                table: "rescue_team_ai_suggestions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "confidence_score",
                table: "mission_ai_suggestions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "confidence_score",
                table: "cluster_ai_analysis",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "confidence_score",
                table: "activity_ai_suggestions",
                type: "double precision",
                nullable: true);
        }
    }
}
