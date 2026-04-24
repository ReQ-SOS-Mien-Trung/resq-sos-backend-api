using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RESQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewSeeds1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateSequence(
                name: "depot_realtime_version_seq");

            migrationBuilder.CreateTable(
                name: "ability_categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ability_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_configs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    temperature = table.Column<double>(type: "double precision", nullable: false),
                    max_tokens = table.Column<int>(type: "integer", nullable: false),
                    api_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    api_key = table.Column<string>(type: "text", nullable: true),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "assembly_points",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    max_capacity = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    location = table.Column<Point>(type: "geography(Point,4326)", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    status_reason = table.Column<string>(type: "text", nullable: true),
                    status_changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status_changed_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assembly_points", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "check_in_radius_configs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    max_radius_meters = table.Column<double>(type: "double precision", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("check_in_radius_configs_pkey", x => x.id);
                    table.CheckConstraint("CK_check_in_radius_configs_max_radius_meters_positive", "\"max_radius_meters\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "depot_realtime_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<int>(type: "integer", nullable: false),
                    mission_id = table.Column<int>(type: "integer", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('depot_realtime_version_seq')"),
                    event_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    operation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    payload_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_critical = table.Column<bool>(type: "boolean", nullable: false),
                    changed_fields = table.Column<string>(type: "text", nullable: true),
                    snapshot_payload = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    lock_owner = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    lock_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("depot_realtime_outbox_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "depots",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    location = table.Column<Point>(type: "geography(Point,4326)", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    capacity = table.Column<decimal>(type: "numeric(14,3)", nullable: true),
                    current_utilization = table.Column<decimal>(type: "numeric(14,3)", nullable: true),
                    weight_capacity = table.Column<decimal>(type: "numeric(14,3)", nullable: true),
                    current_weight_utilization = table.Column<decimal>(type: "numeric(14,3)", nullable: true),
                    advance_limit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    outstanding_advance_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_status_changed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    last_updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_depots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_file_type_categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_file_type_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mission_activity_sync_mutations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    client_mutation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_id = table.Column<int>(type: "integer", nullable: false),
                    activity_id = table.Column<int>(type: "integer", nullable: false),
                    base_server_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    requested_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    effective_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    current_server_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    message = table.Column<string>(type: "text", nullable: true),
                    response_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("mission_activity_sync_mutations_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    content = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "prompts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    prompt_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: true),
                    system_prompt = table.Column<string>(type: "text", nullable: true),
                    user_prompt_template = table.Column<string>(type: "text", nullable: true),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rescue_team_radius_configs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    max_radius_km = table.Column<double>(type: "double precision", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("rescue_team_radius_configs_pkey", x => x.id);
                    table.CheckConstraint("CK_rescue_team_radius_configs_max_radius_km_positive", "\"max_radius_km\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "rescuer_score_visibility_configs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    minimum_evaluation_count = table.Column<int>(type: "integer", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("rescuer_score_visibility_configs_pkey", x => x.id);
                    table.CheckConstraint("CK_rescuer_score_visibility_configs_minimum_evaluation_count_n~", "\"minimum_evaluation_count\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_zones",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    coordinates_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_zones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sos_cluster_grouping_configs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    maximum_distance_km = table.Column<double>(type: "double precision", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("sos_cluster_grouping_configs_pkey", x => x.id);
                    table.CheckConstraint("CK_sos_cluster_grouping_configs_maximum_distance_km_positive", "\"maximum_distance_km\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "sos_clusters",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    center_location = table.Column<Point>(type: "geography(Point,4326)", nullable: true),
                    radius_km = table.Column<double>(type: "double precision", nullable: true),
                    severity_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    water_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    victim_estimated = table.Column<int>(type: "integer", nullable: true),
                    children_count = table.Column<int>(type: "integer", nullable: true),
                    elderly_count = table.Column<int>(type: "integer", nullable: true),
                    medical_urgency_score = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sos_clusters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sos_priority_rule_configs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    config_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    activated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    activated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    config_json = table.Column<string>(type: "jsonb", nullable: false),
                    issue_weights_json = table.Column<string>(type: "jsonb", nullable: false),
                    medical_severe_issues_json = table.Column<string>(type: "jsonb", nullable: false),
                    age_weights_json = table.Column<string>(type: "jsonb", nullable: false),
                    request_type_scores_json = table.Column<string>(type: "jsonb", nullable: false),
                    situation_multipliers_json = table.Column<string>(type: "jsonb", nullable: false),
                    priority_thresholds_json = table.Column<string>(type: "jsonb", nullable: false),
                    water_urgency_scores_json = table.Column<string>(type: "jsonb", nullable: false),
                    food_urgency_scores_json = table.Column<string>(type: "jsonb", nullable: false),
                    blanket_urgency_rules_json = table.Column<string>(type: "jsonb", nullable: false),
                    clothing_urgency_rules_json = table.Column<string>(type: "jsonb", nullable: false),
                    vulnerability_rules_json = table.Column<string>(type: "jsonb", nullable: false),
                    vulnerability_score_expression_json = table.Column<string>(type: "jsonb", nullable: false),
                    relief_score_expression_json = table.Column<string>(type: "jsonb", nullable: false),
                    priority_score_expression_json = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sos_priority_rule_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_warning_band_config",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    bands_json = table.Column<string>(type: "jsonb", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("stock_warning_band_config_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supply_request_priority_configs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    urgent_minutes = table.Column<int>(type: "integer", nullable: false),
                    high_minutes = table.Column<int>(type: "integer", nullable: false),
                    medium_minutes = table.Column<int>(type: "integer", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("supply_request_priority_configs_pkey", x => x.id);
                    table.CheckConstraint("ck_supply_request_priority_configs_order", "urgent_minutes > 0 AND urgent_minutes < high_minutes AND high_minutes < medium_minutes");
                });

            migrationBuilder.CreateTable(
                name: "system_funds",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    balance = table.Column<decimal>(type: "numeric", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("system_funds_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_migration_audit",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    migration_name = table.Column<string>(type: "text", nullable: false),
                    applied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_migration_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "target_groups",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("target_groups_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vat_invoices",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    invoice_serial = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    supplier_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    supplier_tax_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric", nullable: true),
                    file_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vat_invoices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ability_subgroups",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    ability_category_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ability_subgroups", x => x.id);
                    table.ForeignKey(
                        name: "FK_ability_subgroups_ability_categories_ability_category_id",
                        column: x => x.ability_category_id,
                        principalTable: "ability_categories",
                        principalColumn: "id");
                });

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

            migrationBuilder.CreateTable(
                name: "item_models",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category_id = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    item_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    volume_per_unit = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    weight_per_unit = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_models", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_models_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "depot_closures",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    depot_id = table.Column<int>(type: "integer", nullable: false),
                    initiated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    initiated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    previous_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    close_reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    snapshot_consumable_units = table.Column<int>(type: "integer", nullable: false),
                    snapshot_reusable_units = table.Column<int>(type: "integer", nullable: false),
                    actual_consumable_units = table.Column<int>(type: "integer", nullable: true),
                    actual_reusable_units = table.Column<int>(type: "integer", nullable: true),
                    drift_note = table.Column<string>(type: "text", nullable: true),
                    total_consumable_rows = table.Column<int>(type: "integer", nullable: false),
                    processed_consumable_rows = table.Column<int>(type: "integer", nullable: false),
                    last_processed_inventory_id = table.Column<int>(type: "integer", nullable: true),
                    total_reusable_units = table.Column<int>(type: "integer", nullable: false),
                    processed_reusable_units = table.Column<int>(type: "integer", nullable: false),
                    last_processed_reusable_id = table.Column<int>(type: "integer", nullable: true),
                    last_batch_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    target_depot_id = table.Column<int>(type: "integer", nullable: true),
                    external_note = table.Column<string>(type: "text", nullable: true),
                    external_marked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    consumable_zeroed = table.Column<bool>(type: "boolean", nullable: false),
                    reusable_zeroed = table.Column<bool>(type: "boolean", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    max_retries = table.Column<int>(type: "integer", nullable: false),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    is_forced = table.Column<bool>(type: "boolean", nullable: false),
                    force_reason = table.Column<string>(type: "text", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("depot_closures_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_depot_closures_depots_depot_id",
                        column: x => x.depot_id,
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_depot_closures_depots_target_depot_id",
                        column: x => x.target_depot_id,
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "depot_funds",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    depot_id = table.Column<int>(type: "integer", nullable: false),
                    balance = table.Column<decimal>(type: "numeric", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fund_source_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fund_source_id = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("depot_funds_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_depot_funds_depots_depot_id",
                        column: x => x.depot_id,
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_file_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    document_file_type_category_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_file_types", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_file_types_document_file_type_categories_document_~",
                        column: x => x.document_file_type_category_id,
                        principalTable: "document_file_type_categories",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    claim_id = table.Column<int>(type: "integer", nullable: false),
                    is_granted = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("role_permissions_pkey", x => new { x.role_id, x.claim_id });
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_claim_id",
                        column: x => x.claim_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: true),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    password = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    is_email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    avatar_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    email_verification_token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    email_verification_token_expiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    password_reset_token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    password_reset_token_expiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    refresh_token = table.Column<string>(type: "text", nullable: true),
                    refresh_token_expiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    location = table.Column<Point>(type: "geography(Point,4326)", nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ward = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_banned = table.Column<bool>(type: "boolean", nullable: false),
                    banned_by = table.Column<Guid>(type: "uuid", nullable: true),
                    banned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ban_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    assembly_point_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_assembly_points_assembly_point_id",
                        column: x => x.assembly_point_id,
                        principalTable: "assembly_points",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_users_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "activity_ai_suggestions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cluster_id = table.Column<int>(type: "integer", nullable: true),
                    parent_mission_suggestion_id = table.Column<int>(type: "integer", nullable: true),
                    adopted_activity_id = table.Column<int>(type: "integer", nullable: true),
                    model_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    activity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    suggestion_phase = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    suggested_activities = table.Column<string>(type: "jsonb", nullable: true),
                    suggestion_scope = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    adopted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_ai_suggestions", x => x.id);
                    table.ForeignKey(
                        name: "FK_activity_ai_suggestions_sos_clusters_cluster_id",
                        column: x => x.cluster_id,
                        principalTable: "sos_clusters",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "cluster_ai_analysis",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cluster_id = table.Column<int>(type: "integer", nullable: true),
                    model_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    analysis_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    suggested_severity_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    suggested_mission_types = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    suggestion_scope = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    adopted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cluster_ai_analysis", x => x.id);
                    table.ForeignKey(
                        name: "FK_cluster_ai_analysis_sos_clusters_cluster_id",
                        column: x => x.cluster_id,
                        principalTable: "sos_clusters",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "mission_ai_suggestions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cluster_id = table.Column<int>(type: "integer", nullable: true),
                    adopted_mission_id = table.Column<int>(type: "integer", nullable: true),
                    model_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    analysis_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    suggested_mission_title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    suggested_mission_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    suggested_priority_score = table.Column<double>(type: "double precision", nullable: true),
                    suggested_severity_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    suggested_primary_team_id = table.Column<int>(type: "integer", nullable: true),
                    suggested_depot_ids = table.Column<string>(type: "jsonb", nullable: true),
                    suggestion_scope = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    adopted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission_ai_suggestions", x => x.id);
                    table.ForeignKey(
                        name: "FK_mission_ai_suggestions_sos_clusters_cluster_id",
                        column: x => x.cluster_id,
                        principalTable: "sos_clusters",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "system_fund_transactions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    system_fund_id = table.Column<int>(type: "integer", nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    reference_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reference_id = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("system_fund_transactions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_system_fund_transactions_system_funds_system_fund_id",
                        column: x => x.system_fund_id,
                        principalTable: "system_funds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "abilities",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    ability_subgroup_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("abilities_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_abilities_ability_subgroups_ability_subgroup_id",
                        column: x => x.ability_subgroup_id,
                        principalTable: "ability_subgroups",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "inventory_stock_threshold_configs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    scope_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    depot_id = table.Column<int>(type: "integer", nullable: true),
                    category_id = table.Column<int>(type: "integer", nullable: true),
                    item_model_id = table.Column<int>(type: "integer", nullable: true),
                    danger_ratio = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    warning_ratio = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    minimum_threshold = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("inventory_stock_threshold_configs_pkey", x => x.id);
                    table.CheckConstraint("ck_inventory_stock_threshold_configs_ratio", "(danger_ratio IS NULL AND warning_ratio IS NULL) OR (danger_ratio > 0 AND danger_ratio < warning_ratio AND warning_ratio <= 1)");
                    table.CheckConstraint("ck_inventory_stock_threshold_configs_scope", "(scope_type = 'GLOBAL' AND depot_id IS NULL AND category_id IS NULL AND item_model_id IS NULL) OR (scope_type = 'DEPOT' AND depot_id IS NOT NULL AND category_id IS NULL AND item_model_id IS NULL) OR (scope_type = 'DEPOT_CATEGORY' AND depot_id IS NOT NULL AND category_id IS NOT NULL AND item_model_id IS NULL) OR (scope_type = 'DEPOT_ITEM' AND depot_id IS NOT NULL AND category_id IS NULL AND item_model_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_inventory_stock_threshold_configs_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_stock_threshold_configs_depots_depot_id",
                        column: x => x.depot_id,
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_stock_threshold_configs_item_models_item_model_id",
                        column: x => x.item_model_id,
                        principalTable: "item_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_model_target_groups",
                columns: table => new
                {
                    item_model_id = table.Column<int>(type: "integer", nullable: false),
                    target_group_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_model_target_groups", x => new { x.item_model_id, x.target_group_id });
                    table.ForeignKey(
                        name: "FK_item_model_target_groups_item_model_id",
                        column: x => x.item_model_id,
                        principalTable: "item_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_item_model_target_groups_target_group_id",
                        column: x => x.target_group_id,
                        principalTable: "target_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "organization_relief_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    organization_id = table.Column<int>(type: "integer", nullable: true),
                    item_model_id = table.Column<int>(type: "integer", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: true),
                    received_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expired_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    received_by = table.Column<Guid>(type: "uuid", nullable: true),
                    received_at = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_relief_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_organization_relief_items_item_models_item_model_id",
                        column: x => x.item_model_id,
                        principalTable: "item_models",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_organization_relief_items_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "reusable_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    depot_id = table.Column<int>(type: "integer", nullable: true),
                    item_model_id = table.Column<int>(type: "integer", nullable: true),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    condition = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    supply_request_id = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reusable_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_reusable_items_depots_depot_id",
                        column: x => x.depot_id,
                        principalTable: "depots",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_reusable_items_item_models_item_model_id",
                        column: x => x.item_model_id,
                        principalTable: "item_models",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "supply_inventory",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    depot_id = table.Column<int>(type: "integer", nullable: true),
                    item_model_id = table.Column<int>(type: "integer", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: true),
                    mission_reserved_quantity = table.Column<int>(type: "integer", nullable: false),
                    transfer_reserved_quantity = table.Column<int>(type: "integer", nullable: false),
                    last_stocked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supply_inventory", x => x.id);
                    table.ForeignKey(
                        name: "FK_supply_inventory_depots_depot_id",
                        column: x => x.depot_id,
                        principalTable: "depots",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_supply_inventory_item_models_item_model_id",
                        column: x => x.item_model_id,
                        principalTable: "item_models",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "vat_invoice_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vat_invoice_id = table.Column<int>(type: "integer", nullable: true),
                    item_model_id = table.Column<int>(type: "integer", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: true),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vat_invoice_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_vat_invoice_items_item_models_item_model_id",
                        column: x => x.item_model_id,
                        principalTable: "item_models",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_vat_invoice_items_vat_invoices_vat_invoice_id",
                        column: x => x.vat_invoice_id,
                        principalTable: "vat_invoices",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "depot_closure_transfers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    closure_id = table.Column<int>(type: "integer", nullable: false),
                    source_depot_id = table.Column<int>(type: "integer", nullable: false),
                    target_depot_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    transfer_deadline_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    shipped_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    shipped_by = table.Column<Guid>(type: "uuid", nullable: true),
                    ship_note = table.Column<string>(type: "text", nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    received_by = table.Column<Guid>(type: "uuid", nullable: true),
                    receive_note = table.Column<string>(type: "text", nullable: true),
                    snapshot_consumable_units = table.Column<int>(type: "integer", nullable: false),
                    snapshot_reusable_units = table.Column<int>(type: "integer", nullable: false),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("depot_closure_transfers_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_depot_closure_transfers_depot_closures_closure_id",
                        column: x => x.closure_id,
                        principalTable: "depot_closures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "depot_fund_transactions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    depot_fund_id = table.Column<int>(type: "integer", nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    reference_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reference_id = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    contributor_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    contributor_phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    contributor_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("depot_fund_transactions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_depot_fund_transactions_depot_funds_depot_fund_id",
                        column: x => x.depot_fund_id,
                        principalTable: "depot_funds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assembly_events",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    assembly_point_id = table.Column<int>(type: "integer", nullable: false),
                    assembly_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    check_in_deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assembly_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_assembly_events_assembly_points_assembly_point_id",
                        column: x => x.assembly_point_id,
                        principalTable: "assembly_points",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_assembly_events_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "depot_managers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    depot_id = table.Column<int>(type: "integer", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    unassigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: true),
                    unassigned_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_depot_managers", x => x.id);
                    table.ForeignKey(
                        name: "FK_depot_managers_depots_depot_id",
                        column: x => x.depot_id,
                        principalTable: "depots",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_depot_managers_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "depot_supply_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    requesting_depot_id = table.Column<int>(type: "integer", nullable: false),
                    source_depot_id = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    priority_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    requesting_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rejected_reason = table.Column<string>(type: "text", nullable: true),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    auto_reject_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    high_escalation_notified = table.Column<bool>(type: "boolean", nullable: false),
                    high_escalation_notified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    urgent_escalation_notified = table.Column<bool>(type: "boolean", nullable: false),
                    urgent_escalation_notified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    shipped_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    accepted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    prepared_by = table.Column<Guid>(type: "uuid", nullable: true),
                    shipped_by = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_depot_supply_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_depot_supply_requests_depots_requesting_depot_id",
                        column: x => x.requesting_depot_id,
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_depot_supply_requests_depots_source_depot_id",
                        column: x => x.source_depot_id,
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_depot_supply_requests_users_requested_by",
                        column: x => x.requested_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fund_campaigns",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    region = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    campaign_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    campaign_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    target_amount = table.Column<decimal>(type: "numeric", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric", nullable: true),
                    current_balance = table.Column<decimal>(type: "numeric", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    suspend_reason = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    last_modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fund_campaigns", x => x.id);
                    table.ForeignKey(
                        name: "FK_fund_campaigns_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "missions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cluster_id = table.Column<int>(type: "integer", nullable: true),
                    previous_mission_id = table.Column<int>(type: "integer", nullable: true),
                    ai_suggestion_id = table.Column<int>(type: "integer", nullable: true),
                    mission_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    priority_score = table.Column<double>(type: "double precision", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expected_end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_completed = table.Column<bool>(type: "boolean", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    manual_override_metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_missions", x => x.id);
                    table.ForeignKey(
                        name: "FK_missions_missions_previous_mission_id",
                        column: x => x.previous_mission_id,
                        principalTable: "missions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_missions_sos_clusters_cluster_id",
                        column: x => x.cluster_id,
                        principalTable: "sos_clusters",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_missions_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "rescue_teams",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    assembly_point_id = table.Column<int>(type: "integer", nullable: true),
                    managed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    team_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    max_members = table.Column<int>(type: "integer", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    assembly_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    disband_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rescue_teams", x => x.id);
                    table.ForeignKey(
                        name: "FK_rescue_teams_assembly_points_assembly_point_id",
                        column: x => x.assembly_point_id,
                        principalTable: "assembly_points",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_rescue_teams_users_managed_by",
                        column: x => x.managed_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "rescuer_applications",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    admin_note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rescuer_applications", x => x.id);
                    table.ForeignKey(
                        name: "FK_rescuer_applications_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_rescuer_applications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "rescuer_profiles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rescuer_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_eligible_rescuer = table.Column<bool>(type: "boolean", nullable: false),
                    step = table.Column<int>(type: "integer", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("rescuer_profiles_pkey", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_rescuer_profiles_users_approved_by",
                        column: x => x.approved_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_rescuer_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sos_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    packet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cluster_id = table.Column<int>(type: "integer", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    location = table.Column<Point>(type: "geography(Point,4326)", nullable: true),
                    location_accuracy = table.Column<double>(type: "double precision", nullable: true),
                    sos_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    raw_message = table.Column<string>(type: "text", nullable: true),
                    structured_data = table.Column<string>(type: "jsonb", nullable: true),
                    network_metadata = table.Column<string>(type: "jsonb", nullable: true),
                    sender_info = table.Column<string>(type: "jsonb", nullable: true),
                    victim_info = table.Column<string>(type: "jsonb", nullable: true),
                    reporter_info = table.Column<string>(type: "jsonb", nullable: true),
                    is_sent_on_behalf = table.Column<bool>(type: "boolean", nullable: false),
                    origin_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    priority_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    priority_score = table.Column<double>(type: "double precision", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ai_analysis = table.Column<string>(type: "jsonb", nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    timestamp = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_coordinator_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sos_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_sos_requests_sos_clusters_cluster_id",
                        column: x => x.cluster_id,
                        principalTable: "sos_clusters",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_sos_requests_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_sos_requests_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_notifications",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notification_id = table.Column<int>(type: "integer", nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: true),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_notifications_notifications_notification_id",
                        column: x => x.notification_id,
                        principalTable: "notifications",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_user_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_permissions",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<int>(type: "integer", nullable: false),
                    is_granted = table.Column<bool>(type: "boolean", nullable: true),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_permissions_pkey", x => new { x.user_id, x.claim_id });
                    table.ForeignKey(
                        name: "FK_user_permissions_permissions_claim_id",
                        column: x => x.claim_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_permissions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_relative_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    person_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    relation_group = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tags_json = table.Column<string>(type: "jsonb", nullable: false),
                    medical_baseline_note = table.Column<string>(type: "text", nullable: true),
                    special_needs_note = table.Column<string>(type: "text", nullable: true),
                    special_diet_note = table.Column<string>(type: "text", nullable: true),
                    medical_profile_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    profile_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_relative_profiles_pkey", x => x.id);
                    table.CheckConstraint("ck_user_relative_profiles_gender", "gender IS NULL OR gender IN ('MALE','FEMALE')");
                    table.CheckConstraint("ck_user_relative_profiles_person_type", "person_type IN ('ADULT','CHILD','ELDERLY')");
                    table.CheckConstraint("ck_user_relative_profiles_relation_group", "relation_group IN ('gia_dinh','nha_noi','nha_ngoai','hang_xom','ban_be','khac')");
                    table.ForeignKey(
                        name: "FK_user_relative_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_abilities",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ability_id = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_abilities", x => new { x.user_id, x.ability_id });
                    table.ForeignKey(
                        name: "FK_user_abilities_abilities_ability_id",
                        column: x => x.ability_id,
                        principalTable: "abilities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_abilities_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_stock_threshold_config_history",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    config_id = table.Column<int>(type: "integer", nullable: true),
                    scope_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    depot_id = table.Column<int>(type: "integer", nullable: true),
                    category_id = table.Column<int>(type: "integer", nullable: true),
                    item_model_id = table.Column<int>(type: "integer", nullable: true),
                    old_danger_ratio = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    old_warning_ratio = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    new_danger_ratio = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    new_warning_ratio = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    change_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("inventory_stock_threshold_config_history_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_stock_threshold_config_history_inventory_stock_th~",
                        column: x => x.config_id,
                        principalTable: "inventory_stock_threshold_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "supply_inventory_lots",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    supply_inventory_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    remaining_quantity = table.Column<int>(type: "integer", nullable: false),
                    received_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expired_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    source_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("supply_inventory_lots_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_supply_inventory_lots_supply_inventory_supply_inventory_id",
                        column: x => x.supply_inventory_id,
                        principalTable: "supply_inventory",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "depot_closure_transfer_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    transfer_id = table.Column<int>(type: "integer", nullable: false),
                    item_model_id = table.Column<int>(type: "integer", nullable: false),
                    item_name = table.Column<string>(type: "text", nullable: false),
                    item_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("depot_closure_transfer_items_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_depot_closure_transfer_items_depot_closure_transfers_transf~",
                        column: x => x.transfer_id,
                        principalTable: "depot_closure_transfers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "depot_closure_transfer_reusable_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    transfer_id = table.Column<int>(type: "integer", nullable: false),
                    reusable_item_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("depot_closure_transfer_reusable_items_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_depot_closure_transfer_reusable_items_depot_closure_transfe~",
                        column: x => x.transfer_id,
                        principalTable: "depot_closure_transfers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_depot_closure_transfer_reusable_items_reusable_items_reusab~",
                        column: x => x.reusable_item_id,
                        principalTable: "reusable_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assembly_participants",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    assembly_event_id = table.Column<int>(type: "integer", nullable: false),
                    rescuer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_checked_in = table.Column<bool>(type: "boolean", nullable: false),
                    check_in_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_checked_out = table.Column<bool>(type: "boolean", nullable: false),
                    check_out_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assembly_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_assembly_participants_assembly_events_assembly_event_id",
                        column: x => x.assembly_event_id,
                        principalTable: "assembly_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_assembly_participants_users_rescuer_id",
                        column: x => x.rescuer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "depot_supply_request_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    depot_supply_request_id = table.Column<int>(type: "integer", nullable: false),
                    item_model_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_depot_supply_request_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_depot_supply_request_items_depot_supply_requests_depot_supp~",
                        column: x => x.depot_supply_request_id,
                        principalTable: "depot_supply_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_depot_supply_request_items_item_models_item_model_id",
                        column: x => x.item_model_id,
                        principalTable: "item_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "depot_supply_request_reusable_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    supply_request_id = table.Column<int>(type: "integer", nullable: false),
                    reusable_item_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("depot_supply_request_reusable_items_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_depot_supply_request_reusable_items_depot_supply_requests_s~",
                        column: x => x.supply_request_id,
                        principalTable: "depot_supply_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_depot_supply_request_reusable_items_reusable_items_reusable~",
                        column: x => x.reusable_item_id,
                        principalTable: "reusable_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "donations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fund_campaign_id = table.Column<int>(type: "integer", nullable: true),
                    donor_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    donor_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    amount = table.Column<decimal>(type: "numeric", nullable: true),
                    order_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    payment_method_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    payment_audit_info = table.Column<string>(type: "text", nullable: true),
                    is_private = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    response_deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_donations", x => x.id);
                    table.ForeignKey(
                        name: "FK_donations_fund_campaigns_fund_campaign_id",
                        column: x => x.fund_campaign_id,
                        principalTable: "fund_campaigns",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "fund_transactions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fund_campaign_id = table.Column<int>(type: "integer", nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    direction = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    amount = table.Column<decimal>(type: "numeric", nullable: true),
                    reference_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reference_id = table.Column<int>(type: "integer", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fund_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_fund_transactions_fund_campaigns_fund_campaign_id",
                        column: x => x.fund_campaign_id,
                        principalTable: "fund_campaigns",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_fund_transactions_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "funding_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    depot_id = table.Column<int>(type: "integer", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    attachment_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    approved_campaign_id = table.Column<int>(type: "integer", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funding_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_funding_requests_depots_depot_id",
                        column: x => x.depot_id,
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_funding_requests_fund_campaigns_approved_campaign_id",
                        column: x => x.approved_campaign_id,
                        principalTable: "fund_campaigns",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_funding_requests_users_requested_by",
                        column: x => x.requested_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_funding_requests_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    victim_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mission_id = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    selected_topic = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    linked_sos_request_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.id);
                    table.ForeignKey(
                        name: "FK_conversations_missions_mission_id",
                        column: x => x.mission_id,
                        principalTable: "missions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_conversations_users_victim_id",
                        column: x => x.victim_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "mission_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    item_model_id = table.Column<int>(type: "integer", nullable: true),
                    mission_id = table.Column<int>(type: "integer", nullable: true),
                    required_quantity = table.Column<int>(type: "integer", nullable: true),
                    allocated_quantity = table.Column<int>(type: "integer", nullable: true),
                    source_depot_id = table.Column<int>(type: "integer", nullable: true),
                    buffer_ratio = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_mission_items_depots_source_depot_id",
                        column: x => x.source_depot_id,
                        principalTable: "depots",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_mission_items_item_models_item_model_id",
                        column: x => x.item_model_id,
                        principalTable: "item_models",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_mission_items_missions_mission_id",
                        column: x => x.mission_id,
                        principalTable: "missions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "mission_teams",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mission_id = table.Column<int>(type: "integer", nullable: true),
                    rescuer_team_id = table.Column<int>(type: "integer", nullable: true),
                    team_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    current_location = table.Column<Point>(type: "geography(Point,4326)", nullable: true),
                    location_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    location_source = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    unassigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission_teams", x => x.id);
                    table.ForeignKey(
                        name: "FK_mission_teams_missions_mission_id",
                        column: x => x.mission_id,
                        principalTable: "missions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_mission_teams_rescue_teams_rescuer_team_id",
                        column: x => x.rescuer_team_id,
                        principalTable: "rescue_teams",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "rescue_team_ai_suggestions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cluster_id = table.Column<int>(type: "integer", nullable: true),
                    adopted_rescue_team_id = table.Column<int>(type: "integer", nullable: true),
                    model_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    analysis_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    suggested_members = table.Column<string>(type: "jsonb", nullable: true),
                    suggestion_scope = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    adopted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rescue_team_ai_suggestions", x => x.id);
                    table.ForeignKey(
                        name: "FK_rescue_team_ai_suggestions_rescue_teams_adopted_rescue_team~",
                        column: x => x.adopted_rescue_team_id,
                        principalTable: "rescue_teams",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_rescue_team_ai_suggestions_sos_clusters_cluster_id",
                        column: x => x.cluster_id,
                        principalTable: "sos_clusters",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "rescue_team_members",
                columns: table => new
                {
                    team_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    invited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_leader = table.Column<bool>(type: "boolean", nullable: false),
                    role_in_team = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    checked_in = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rescue_team_members", x => new { x.team_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_rescue_team_members_rescue_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "rescue_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rescue_team_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rescuer_application_documents",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    application_id = table.Column<int>(type: "integer", nullable: true),
                    file_url = table.Column<string>(type: "text", nullable: true),
                    file_type_id = table.Column<int>(type: "integer", nullable: true),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rescuer_application_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_rescuer_application_documents_document_file_types_file_type~",
                        column: x => x.file_type_id,
                        principalTable: "document_file_types",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_rescuer_application_documents_rescuer_applications_applicat~",
                        column: x => x.application_id,
                        principalTable: "rescuer_applications",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "rescuer_scores",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    response_time_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    rescue_effectiveness_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    decision_handling_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    safety_medical_skill_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    teamwork_communication_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    overall_average_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    evaluation_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("rescuer_scores_pkey", x => x.user_id);
                    table.CheckConstraint("CK_rescuer_scores_decision_handling_score_range", "\"decision_handling_score\" >= 0 AND \"decision_handling_score\" <= 10");
                    table.CheckConstraint("CK_rescuer_scores_evaluation_count_non_negative", "\"evaluation_count\" >= 0");
                    table.CheckConstraint("CK_rescuer_scores_overall_average_score_range", "\"overall_average_score\" >= 0 AND \"overall_average_score\" <= 10");
                    table.CheckConstraint("CK_rescuer_scores_rescue_effectiveness_score_range", "\"rescue_effectiveness_score\" >= 0 AND \"rescue_effectiveness_score\" <= 10");
                    table.CheckConstraint("CK_rescuer_scores_response_time_score_range", "\"response_time_score\" >= 0 AND \"response_time_score\" <= 10");
                    table.CheckConstraint("CK_rescuer_scores_safety_medical_skill_score_range", "\"safety_medical_skill_score\" >= 0 AND \"safety_medical_skill_score\" <= 10");
                    table.CheckConstraint("CK_rescuer_scores_teamwork_communication_score_range", "\"teamwork_communication_score\" >= 0 AND \"teamwork_communication_score\" <= 10");
                    table.ForeignKey(
                        name: "FK_rescuer_scores_rescuer_profiles_user_id",
                        column: x => x.user_id,
                        principalTable: "rescuer_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sos_ai_analysis",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sos_request_id = table.Column<int>(type: "integer", nullable: true),
                    model_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    analysis_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    suggested_severity_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    suggested_priority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    suggested_priority_score = table.Column<double>(type: "double precision", nullable: true),
                    agrees_with_rule_base = table.Column<bool>(type: "boolean", nullable: true),
                    explanation = table.Column<string>(type: "text", nullable: true),
                    suggestion_scope = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    adopted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sos_ai_analysis", x => x.id);
                    table.ForeignKey(
                        name: "FK_sos_ai_analysis_sos_requests_sos_request_id",
                        column: x => x.sos_request_id,
                        principalTable: "sos_requests",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "sos_request_companions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sos_request_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sos_request_companions", x => x.id);
                    table.ForeignKey(
                        name: "FK_sos_request_companions_sos_requests_sos_request_id",
                        column: x => x.sos_request_id,
                        principalTable: "sos_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sos_request_companions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sos_request_updates",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sos_request_id = table.Column<int>(type: "integer", nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    content = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sos_request_updates", x => x.id);
                    table.ForeignKey(
                        name: "FK_sos_request_updates_sos_requests_sos_request_id",
                        column: x => x.sos_request_id,
                        principalTable: "sos_requests",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "sos_rule_evaluations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sos_request_id = table.Column<int>(type: "integer", nullable: true),
                    config_id = table.Column<int>(type: "integer", nullable: true),
                    config_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    medical_score = table.Column<double>(type: "double precision", nullable: true),
                    food_score = table.Column<double>(type: "double precision", nullable: true),
                    injury_score = table.Column<double>(type: "double precision", nullable: true),
                    mobility_score = table.Column<double>(type: "double precision", nullable: true),
                    environment_score = table.Column<double>(type: "double precision", nullable: true),
                    total_score = table.Column<double>(type: "double precision", nullable: true),
                    priority_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    rule_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    items_needed = table.Column<string>(type: "jsonb", nullable: true),
                    breakdown_json = table.Column<string>(type: "jsonb", nullable: true),
                    details_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sos_rule_evaluations", x => x.id);
                    table.ForeignKey(
                        name: "FK_sos_rule_evaluations_sos_requests_sos_request_id",
                        column: x => x.sos_request_id,
                        principalTable: "sos_requests",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "depot_closure_external_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    depot_id = table.Column<int>(type: "integer", nullable: false),
                    closure_id = table.Column<int>(type: "integer", nullable: true),
                    item_model_id = table.Column<int>(type: "integer", nullable: true),
                    lot_id = table.Column<int>(type: "integer", nullable: true),
                    reusable_item_id = table.Column<int>(type: "integer", nullable: true),
                    item_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    category_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    item_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    total_price = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    handling_method = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    recipient = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    processed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("depot_closure_external_items_pkey", x => x.id);
                    table.CheckConstraint("CK_ClosureExternalItem_ConsumableOrReusable", "((\"item_type\" = 'Consumable' AND \"lot_id\" IS NOT NULL AND \"reusable_item_id\" IS NULL AND \"serial_number\" IS NULL) OR (\"item_type\" = 'Reusable' AND \"lot_id\" IS NULL AND \"reusable_item_id\" IS NOT NULL AND \"serial_number\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_depot_closure_external_items_depot_closures_closure_id",
                        column: x => x.closure_id,
                        principalTable: "depot_closures",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_depot_closure_external_items_depots_depot_id",
                        column: x => x.depot_id,
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_depot_closure_external_items_item_models_item_model_id",
                        column: x => x.item_model_id,
                        principalTable: "item_models",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_depot_closure_external_items_reusable_items_reusable_item_id",
                        column: x => x.reusable_item_id,
                        principalTable: "reusable_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_depot_closure_external_items_supply_inventory_lots_lot_id",
                        column: x => x.lot_id,
                        principalTable: "supply_inventory_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "depot_closure_transfer_consumable_reservations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    transfer_id = table.Column<int>(type: "integer", nullable: false),
                    supply_inventory_id = table.Column<int>(type: "integer", nullable: false),
                    supply_inventory_lot_id = table.Column<int>(type: "integer", nullable: true),
                    item_model_id = table.Column<int>(type: "integer", nullable: false),
                    reserved_quantity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    received_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expired_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("depot_closure_transfer_consumable_reservations_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_depot_closure_transfer_consumable_reservations_depot_closur~",
                        column: x => x.transfer_id,
                        principalTable: "depot_closure_transfers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_depot_closure_transfer_consumable_reservations_supply_inven~",
                        column: x => x.supply_inventory_id,
                        principalTable: "supply_inventory",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_depot_closure_transfer_consumable_reservations_supply_inve~1",
                        column: x => x.supply_inventory_lot_id,
                        principalTable: "supply_inventory_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "depot_supply_request_consumable_reservations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    supply_request_id = table.Column<int>(type: "integer", nullable: false),
                    supply_inventory_id = table.Column<int>(type: "integer", nullable: false),
                    supply_inventory_lot_id = table.Column<int>(type: "integer", nullable: true),
                    item_model_id = table.Column<int>(type: "integer", nullable: false),
                    reserved_quantity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    received_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expired_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("depot_supply_request_consumable_reservations_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_depot_supply_request_consumable_reservations_depot_supply_r~",
                        column: x => x.supply_request_id,
                        principalTable: "depot_supply_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_depot_supply_request_consumable_reservations_supply_invento~",
                        column: x => x.supply_inventory_id,
                        principalTable: "supply_inventory",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_depot_supply_request_consumable_reservations_supply_invent~1",
                        column: x => x.supply_inventory_lot_id,
                        principalTable: "supply_inventory_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'1000', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    depot_supply_inventory_id = table.Column<int>(type: "integer", nullable: true),
                    reusable_item_id = table.Column<int>(type: "integer", nullable: true),
                    vat_invoice_id = table.Column<int>(type: "integer", nullable: true),
                    action_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    quantity_change = table.Column<int>(type: "integer", nullable: true),
                    source_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_id = table.Column<int>(type: "integer", nullable: true),
                    mission_id = table.Column<int>(type: "integer", nullable: true),
                    performed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    received_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expired_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    supply_inventory_lot_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_logs_reusable_items_reusable_item_id",
                        column: x => x.reusable_item_id,
                        principalTable: "reusable_items",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_inventory_logs_supply_inventory_depot_supply_inventory_id",
                        column: x => x.depot_supply_inventory_id,
                        principalTable: "supply_inventory",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_inventory_logs_supply_inventory_lots_supply_inventory_lot_id",
                        column: x => x.supply_inventory_lot_id,
                        principalTable: "supply_inventory_lots",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_inventory_logs_users_performed_by",
                        column: x => x.performed_by,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_inventory_logs_vat_invoices_vat_invoice_id",
                        column: x => x.vat_invoice_id,
                        principalTable: "vat_invoices",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "campaign_disbursements",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fund_campaign_id = table.Column<int>(type: "integer", nullable: false),
                    depot_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    purpose = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    funding_request_id = table.Column<int>(type: "integer", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_disbursements", x => x.id);
                    table.ForeignKey(
                        name: "FK_campaign_disbursements_depots_depot_id",
                        column: x => x.depot_id,
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_campaign_disbursements_fund_campaigns_fund_campaign_id",
                        column: x => x.fund_campaign_id,
                        principalTable: "fund_campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_campaign_disbursements_funding_requests_funding_request_id",
                        column: x => x.funding_request_id,
                        principalTable: "funding_requests",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_campaign_disbursements_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "funding_request_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    funding_request_id = table.Column<int>(type: "integer", nullable: false),
                    row = table.Column<int>(type: "integer", nullable: false),
                    item_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    category_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric", nullable: false),
                    total_price = table.Column<decimal>(type: "numeric", nullable: false),
                    item_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_group = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    received_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expired_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    volume_per_unit = table.Column<decimal>(type: "numeric", nullable: false),
                    weight_per_unit = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funding_request_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_funding_request_items_funding_requests_funding_request_id",
                        column: x => x.funding_request_id,
                        principalTable: "funding_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "conversation_participants",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conversation_id = table.Column<int>(type: "integer", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    role_in_conversation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    left_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_conversation_participants_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_conversation_participants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conversation_id = table.Column<int>(type: "integer", nullable: true),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content = table.Column<string>(type: "text", nullable: true),
                    message_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_messages_users_sender_id",
                        column: x => x.sender_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "mission_activities",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mission_id = table.Column<int>(type: "integer", nullable: true),
                    step = table.Column<int>(type: "integer", nullable: true),
                    activity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    target = table.Column<string>(type: "jsonb", nullable: true),
                    items = table.Column<string>(type: "jsonb", nullable: true),
                    target_location = table.Column<Point>(type: "geography(Point,4326)", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_decision_by = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    mission_team_id = table.Column<int>(type: "integer", nullable: true),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    estimated_time = table.Column<int>(type: "integer", nullable: true),
                    sos_request_id = table.Column<int>(type: "integer", nullable: true),
                    depot_id = table.Column<int>(type: "integer", nullable: true),
                    depot_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    depot_address = table.Column<string>(type: "text", nullable: true),
                    assembly_point_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission_activities", x => x.id);
                    table.ForeignKey(
                        name: "FK_mission_activities_assembly_points_assembly_point_id",
                        column: x => x.assembly_point_id,
                        principalTable: "assembly_points",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_mission_activities_mission_teams_mission_team_id",
                        column: x => x.mission_team_id,
                        principalTable: "mission_teams",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_mission_activities_missions_mission_id",
                        column: x => x.mission_id,
                        principalTable: "missions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_mission_activities_users_completed_by",
                        column: x => x.completed_by,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_mission_activities_users_last_decision_by",
                        column: x => x.last_decision_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "mission_team_members",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mission_team_id = table.Column<int>(type: "integer", nullable: true),
                    rescuer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    role_in_team = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    left_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission_team_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_mission_team_members_mission_teams_mission_team_id",
                        column: x => x.mission_team_id,
                        principalTable: "mission_teams",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_mission_team_members_users_rescuer_id",
                        column: x => x.rescuer_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "mission_team_reports",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mission_team_id = table.Column<int>(type: "integer", nullable: false),
                    report_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    team_summary = table.Column<string>(type: "text", nullable: true),
                    team_note = table.Column<string>(type: "text", nullable: true),
                    issues_json = table.Column<string>(type: "jsonb", nullable: true),
                    result_json = table.Column<string>(type: "jsonb", nullable: true),
                    evidence_json = table.Column<string>(type: "jsonb", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_edited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("mission_team_reports_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_mission_team_reports_mission_teams_mission_team_id",
                        column: x => x.mission_team_id,
                        principalTable: "mission_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mission_team_reports_users_submitted_by",
                        column: x => x.submitted_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "disbursement_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    campaign_disbursement_id = table.Column<int>(type: "integer", nullable: false),
                    item_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric", nullable: false),
                    total_price = table.Column<decimal>(type: "numeric", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disbursement_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_disbursement_items_campaign_disbursements_campaign_disburse~",
                        column: x => x.campaign_disbursement_id,
                        principalTable: "campaign_disbursements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_incidents",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mission_team_id = table.Column<int>(type: "integer", nullable: true),
                    mission_activity_id = table.Column<int>(type: "integer", nullable: true),
                    location = table.Column<Point>(type: "geography(Point,4326)", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    incident_scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    incident_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    decision_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    detail_json = table.Column<string>(type: "jsonb", nullable: true),
                    payload_version = table.Column<int>(type: "integer", nullable: true),
                    need_support_sos = table.Column<bool>(type: "boolean", nullable: true),
                    need_reassign_activity = table.Column<bool>(type: "boolean", nullable: true),
                    support_sos_request_id = table.Column<int>(type: "integer", nullable: true),
                    reported_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_incidents", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_incidents_mission_activities_mission_activity_id",
                        column: x => x.mission_activity_id,
                        principalTable: "mission_activities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_team_incidents_mission_teams_mission_team_id",
                        column: x => x.mission_team_id,
                        principalTable: "mission_teams",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_team_incidents_sos_requests_support_sos_request_id",
                        column: x => x.support_sos_request_id,
                        principalTable: "sos_requests",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "mission_activity_reports",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mission_team_report_id = table.Column<int>(type: "integer", nullable: false),
                    mission_activity_id = table.Column<int>(type: "integer", nullable: false),
                    activity_type = table.Column<string>(type: "text", nullable: true),
                    execution_status = table.Column<string>(type: "text", nullable: true),
                    summary = table.Column<string>(type: "text", nullable: true),
                    issues_json = table.Column<string>(type: "jsonb", nullable: true),
                    result_json = table.Column<string>(type: "jsonb", nullable: true),
                    evidence_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("mission_activity_reports_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_mission_activity_reports_mission_activities_mission_activit~",
                        column: x => x.mission_activity_id,
                        principalTable: "mission_activities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mission_activity_reports_mission_team_reports_mission_team_~",
                        column: x => x.mission_team_report_id,
                        principalTable: "mission_team_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mission_team_member_evaluations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mission_team_report_id = table.Column<int>(type: "integer", nullable: false),
                    rescuer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    response_time_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    rescue_effectiveness_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    decision_handling_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    safety_medical_skill_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    teamwork_communication_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("mission_team_member_evaluations_pkey", x => x.id);
                    table.CheckConstraint("CK_mission_team_member_evaluations_decision_handling_score_ran~", "\"decision_handling_score\" >= 0 AND \"decision_handling_score\" <= 10");
                    table.CheckConstraint("CK_mission_team_member_evaluations_rescue_effectiveness_score_~", "\"rescue_effectiveness_score\" >= 0 AND \"rescue_effectiveness_score\" <= 10");
                    table.CheckConstraint("CK_mission_team_member_evaluations_response_time_score_range", "\"response_time_score\" >= 0 AND \"response_time_score\" <= 10");
                    table.CheckConstraint("CK_mission_team_member_evaluations_safety_medical_skill_score_~", "\"safety_medical_skill_score\" >= 0 AND \"safety_medical_skill_score\" <= 10");
                    table.CheckConstraint("CK_mission_team_member_evaluations_teamwork_communication_scor~", "\"teamwork_communication_score\" >= 0 AND \"teamwork_communication_score\" <= 10");
                    table.ForeignKey(
                        name: "FK_mission_team_member_evaluations_mission_team_reports_missio~",
                        column: x => x.mission_team_report_id,
                        principalTable: "mission_team_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mission_team_member_evaluations_rescuer_profiles_rescuer_id",
                        column: x => x.rescuer_id,
                        principalTable: "rescuer_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_incident_activities",
                columns: table => new
                {
                    team_incident_id = table.Column<int>(type: "integer", nullable: false),
                    mission_activity_id = table.Column<int>(type: "integer", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("team_incident_activities_pkey", x => new { x.team_incident_id, x.mission_activity_id });
                    table.ForeignKey(
                        name: "fk_team_incident_activities_mission_activity_id",
                        column: x => x.mission_activity_id,
                        principalTable: "mission_activities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_team_incident_activities_team_incident_id",
                        column: x => x.team_incident_id,
                        principalTable: "team_incidents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "abilities_code_key",
                table: "abilities",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_abilities_ability_subgroup_id",
                table: "abilities",
                column: "ability_subgroup_id");

            migrationBuilder.CreateIndex(
                name: "ability_categories_code_key",
                table: "ability_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ability_subgroups_code_key",
                table: "ability_subgroups",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ability_subgroups_ability_category_id",
                table: "ability_subgroups",
                column: "ability_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_activity_ai_suggestions_cluster_id",
                table: "activity_ai_suggestions",
                column: "cluster_id");

            migrationBuilder.CreateIndex(
                name: "IX_assembly_events_assembly_point_id",
                table: "assembly_events",
                column: "assembly_point_id");

            migrationBuilder.CreateIndex(
                name: "IX_assembly_events_created_by",
                table: "assembly_events",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_assembly_participants_assembly_event_id",
                table: "assembly_participants",
                column: "assembly_event_id");

            migrationBuilder.CreateIndex(
                name: "IX_assembly_participants_rescuer_id",
                table: "assembly_participants",
                column: "rescuer_id");

            migrationBuilder.CreateIndex(
                name: "IX_assembly_point_check_in_radius_configs_assembly_point_id",
                table: "assembly_point_check_in_radius_configs",
                column: "assembly_point_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_campaign_disbursements_created_by",
                table: "campaign_disbursements",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_campaign_disbursements_depot_id",
                table: "campaign_disbursements",
                column: "depot_id");

            migrationBuilder.CreateIndex(
                name: "IX_campaign_disbursements_fund_campaign_id",
                table: "campaign_disbursements",
                column: "fund_campaign_id");

            migrationBuilder.CreateIndex(
                name: "IX_campaign_disbursements_funding_request_id",
                table: "campaign_disbursements",
                column: "funding_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cluster_ai_analysis_cluster_id",
                table: "cluster_ai_analysis",
                column: "cluster_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_participants_conversation_id",
                table: "conversation_participants",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_participants_user_id",
                table: "conversation_participants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_mission_id",
                table: "conversations",
                column: "mission_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_victim_id",
                table: "conversations",
                column: "victim_id");

            migrationBuilder.CreateIndex(
                name: "ix_depot_closure_external_items_closure_id",
                table: "depot_closure_external_items",
                column: "closure_id");

            migrationBuilder.CreateIndex(
                name: "ix_depot_closure_external_items_depot_id",
                table: "depot_closure_external_items",
                column: "depot_id");

            migrationBuilder.CreateIndex(
                name: "IX_depot_closure_external_items_item_model_id",
                table: "depot_closure_external_items",
                column: "item_model_id");

            migrationBuilder.CreateIndex(
                name: "IX_depot_closure_external_items_lot_id",
                table: "depot_closure_external_items",
                column: "lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_depot_closure_external_items_reusable_item_id",
                table: "depot_closure_external_items",
                column: "reusable_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_depot_closure_transfer_consumable_reservations_inventory_id",
                table: "depot_closure_transfer_consumable_reservations",
                column: "supply_inventory_id");

            migrationBuilder.CreateIndex(
                name: "ix_depot_closure_transfer_consumable_reservations_lot_id",
                table: "depot_closure_transfer_consumable_reservations",
                column: "supply_inventory_lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_depot_closure_transfer_consumable_reservations_transfer_id",
                table: "depot_closure_transfer_consumable_reservations",
                column: "transfer_id");

            migrationBuilder.CreateIndex(
                name: "ix_depot_closure_transfer_items_transfer_id",
                table: "depot_closure_transfer_items",
                column: "transfer_id");

            migrationBuilder.CreateIndex(
                name: "ix_depot_closure_transfer_reusable_items_reusable_item_id",
                table: "depot_closure_transfer_reusable_items",
                column: "reusable_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_depot_closure_transfer_reusable_items_transfer_id",
                table: "depot_closure_transfer_reusable_items",
                column: "transfer_id");

            migrationBuilder.CreateIndex(
                name: "ux_depot_closure_transfer_reusable_items_transfer_reusable",
                table: "depot_closure_transfer_reusable_items",
                columns: new[] { "transfer_id", "reusable_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uix_depot_closure_transfers_active",
                table: "depot_closure_transfers",
                column: "closure_id",
                filter: "status NOT IN ('Received', 'Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_depot_closures_target_depot_id",
                table: "depot_closures",
                column: "target_depot_id");

            migrationBuilder.CreateIndex(
                name: "uix_depot_closures_active",
                table: "depot_closures",
                column: "depot_id",
                unique: true,
                filter: "status IN ('InProgress', 'Processing')");

            migrationBuilder.CreateIndex(
                name: "ix_depot_fund_transactions_contributor_phone_type",
                table: "depot_fund_transactions",
                columns: new[] { "contributor_phone_number", "transaction_type" });

            migrationBuilder.CreateIndex(
                name: "ix_depot_fund_transactions_fund_created_at",
                table: "depot_fund_transactions",
                columns: new[] { "depot_fund_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "uix_depot_funds_depot_source",
                table: "depot_funds",
                columns: new[] { "depot_id", "fund_source_type", "fund_source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_depot_managers_depot_id",
                table: "depot_managers",
                column: "depot_id");

            migrationBuilder.CreateIndex(
                name: "IX_depot_managers_user_id",
                table: "depot_managers",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_depot_realtime_outbox_depot_version",
                table: "depot_realtime_outbox",
                columns: new[] { "depot_id", "version" });

            migrationBuilder.CreateIndex(
                name: "ix_depot_realtime_outbox_status_next_attempt",
                table: "depot_realtime_outbox",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_depot_supply_request_consumable_reservations_inventory_id",
                table: "depot_supply_request_consumable_reservations",
                column: "supply_inventory_id");

            migrationBuilder.CreateIndex(
                name: "ix_depot_supply_request_consumable_reservations_lot_id",
                table: "depot_supply_request_consumable_reservations",
                column: "supply_inventory_lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_depot_supply_request_consumable_reservations_supply_request_id",
                table: "depot_supply_request_consumable_reservations",
                column: "supply_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_depot_supply_request_items_depot_supply_request_id",
                table: "depot_supply_request_items",
                column: "depot_supply_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_depot_supply_request_items_item_model_id",
                table: "depot_supply_request_items",
                column: "item_model_id");

            migrationBuilder.CreateIndex(
                name: "ix_depot_supply_request_reusable_items_reusable_item_id",
                table: "depot_supply_request_reusable_items",
                column: "reusable_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_depot_supply_request_reusable_items_supply_request_id",
                table: "depot_supply_request_reusable_items",
                column: "supply_request_id");

            migrationBuilder.CreateIndex(
                name: "ux_depot_supply_request_reusable_items_request_reusable",
                table: "depot_supply_request_reusable_items",
                columns: new[] { "supply_request_id", "reusable_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_depot_supply_requests_requested_by",
                table: "depot_supply_requests",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "IX_depot_supply_requests_requesting_depot_id",
                table: "depot_supply_requests",
                column: "requesting_depot_id");

            migrationBuilder.CreateIndex(
                name: "IX_depot_supply_requests_source_depot_id",
                table: "depot_supply_requests",
                column: "source_depot_id");

            migrationBuilder.CreateIndex(
                name: "IX_disbursement_items_campaign_disbursement_id",
                table: "disbursement_items",
                column: "campaign_disbursement_id");

            migrationBuilder.CreateIndex(
                name: "document_file_type_categories_code_key",
                table: "document_file_type_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "document_file_types_code_key",
                table: "document_file_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_file_types_document_file_type_category_id",
                table: "document_file_types",
                column: "document_file_type_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_donations_fund_campaign_id",
                table: "donations",
                column: "fund_campaign_id");

            migrationBuilder.CreateIndex(
                name: "IX_fund_campaigns_created_by",
                table: "fund_campaigns",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_fund_transactions_created_by",
                table: "fund_transactions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_fund_transactions_fund_campaign_id",
                table: "fund_transactions",
                column: "fund_campaign_id");

            migrationBuilder.CreateIndex(
                name: "IX_funding_request_items_funding_request_id",
                table: "funding_request_items",
                column: "funding_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_funding_requests_approved_campaign_id",
                table: "funding_requests",
                column: "approved_campaign_id");

            migrationBuilder.CreateIndex(
                name: "IX_funding_requests_depot_id",
                table: "funding_requests",
                column: "depot_id");

            migrationBuilder.CreateIndex(
                name: "IX_funding_requests_requested_by",
                table: "funding_requests",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "IX_funding_requests_reviewed_by",
                table: "funding_requests",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_logs_depot_supply_inventory_id",
                table: "inventory_logs",
                column: "depot_supply_inventory_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_logs_performed_by",
                table: "inventory_logs",
                column: "performed_by");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_logs_reusable_item_id",
                table: "inventory_logs",
                column: "reusable_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_logs_supply_inventory_lot_id",
                table: "inventory_logs",
                column: "supply_inventory_lot_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_logs_vat_invoice_id",
                table: "inventory_logs",
                column: "vat_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stock_threshold_config_history_config_id",
                table: "inventory_stock_threshold_config_history",
                column: "config_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stock_threshold_configs_category_id",
                table: "inventory_stock_threshold_configs",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stock_threshold_configs_depot_id",
                table: "inventory_stock_threshold_configs",
                column: "depot_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stock_threshold_configs_item_model_id",
                table: "inventory_stock_threshold_configs",
                column: "item_model_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_threshold_depot_active",
                table: "inventory_stock_threshold_configs",
                columns: new[] { "scope_type", "depot_id" },
                unique: true,
                filter: "scope_type = 'DEPOT' AND is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_stock_threshold_depot_category_active",
                table: "inventory_stock_threshold_configs",
                columns: new[] { "scope_type", "depot_id", "category_id" },
                unique: true,
                filter: "scope_type = 'DEPOT_CATEGORY' AND is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_stock_threshold_depot_item_active",
                table: "inventory_stock_threshold_configs",
                columns: new[] { "scope_type", "depot_id", "item_model_id" },
                unique: true,
                filter: "scope_type = 'DEPOT_ITEM' AND is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_stock_threshold_global_active",
                table: "inventory_stock_threshold_configs",
                column: "scope_type",
                unique: true,
                filter: "scope_type = 'GLOBAL' AND is_active = true");

            migrationBuilder.CreateIndex(
                name: "IX_item_model_target_groups_target_group_id",
                table: "item_model_target_groups",
                column: "target_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_item_models_category_id",
                table: "item_models",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_messages_conversation_id",
                table: "messages",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "IX_messages_sender_id",
                table: "messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_activities_assembly_point_id",
                table: "mission_activities",
                column: "assembly_point_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_activities_completed_by",
                table: "mission_activities",
                column: "completed_by");

            migrationBuilder.CreateIndex(
                name: "IX_mission_activities_last_decision_by",
                table: "mission_activities",
                column: "last_decision_by");

            migrationBuilder.CreateIndex(
                name: "IX_mission_activities_mission_id",
                table: "mission_activities",
                column: "mission_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_activities_mission_team_id",
                table: "mission_activities",
                column: "mission_team_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_activity_reports_mission_activity_id",
                table: "mission_activity_reports",
                column: "mission_activity_id");

            migrationBuilder.CreateIndex(
                name: "ux_mission_activity_reports_team_report_activity",
                table: "mission_activity_reports",
                columns: new[] { "mission_team_report_id", "mission_activity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_mission_activity_sync_mutations_client_mutation_id",
                table: "mission_activity_sync_mutations",
                column: "client_mutation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mission_ai_suggestions_cluster_id",
                table: "mission_ai_suggestions",
                column: "cluster_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_items_item_model_id",
                table: "mission_items",
                column: "item_model_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_items_mission_id",
                table: "mission_items",
                column: "mission_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_items_source_depot_id",
                table: "mission_items",
                column: "source_depot_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_team_member_evaluations_rescuer_id",
                table: "mission_team_member_evaluations",
                column: "rescuer_id");

            migrationBuilder.CreateIndex(
                name: "ux_mission_team_member_evaluations_report_rescuer",
                table: "mission_team_member_evaluations",
                columns: new[] { "mission_team_report_id", "rescuer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mission_team_members_mission_team_id",
                table: "mission_team_members",
                column: "mission_team_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_team_members_rescuer_id",
                table: "mission_team_members",
                column: "rescuer_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_team_reports_submitted_by",
                table: "mission_team_reports",
                column: "submitted_by");

            migrationBuilder.CreateIndex(
                name: "ux_mission_team_reports_mission_team_id",
                table: "mission_team_reports",
                column: "mission_team_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mission_teams_mission_id",
                table: "mission_teams",
                column: "mission_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_teams_rescuer_team_id",
                table: "mission_teams",
                column: "rescuer_team_id");

            migrationBuilder.CreateIndex(
                name: "IX_missions_cluster_id",
                table: "missions",
                column: "cluster_id");

            migrationBuilder.CreateIndex(
                name: "IX_missions_created_by",
                table: "missions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_missions_previous_mission_id",
                table: "missions",
                column: "previous_mission_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_relief_items_item_model_id",
                table: "organization_relief_items",
                column: "item_model_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_relief_items_organization_id",
                table: "organization_relief_items",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_rescue_team_ai_suggestions_adopted_rescue_team_id",
                table: "rescue_team_ai_suggestions",
                column: "adopted_rescue_team_id");

            migrationBuilder.CreateIndex(
                name: "IX_rescue_team_ai_suggestions_cluster_id",
                table: "rescue_team_ai_suggestions",
                column: "cluster_id");

            migrationBuilder.CreateIndex(
                name: "IX_rescue_team_members_user_id",
                table: "rescue_team_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_rescue_teams_assembly_point_id",
                table: "rescue_teams",
                column: "assembly_point_id");

            migrationBuilder.CreateIndex(
                name: "IX_rescue_teams_managed_by",
                table: "rescue_teams",
                column: "managed_by");

            migrationBuilder.CreateIndex(
                name: "IX_rescuer_application_documents_application_id",
                table: "rescuer_application_documents",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_rescuer_application_documents_file_type_id",
                table: "rescuer_application_documents",
                column: "file_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_rescuer_applications_reviewed_by",
                table: "rescuer_applications",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_rescuer_applications_user_id",
                table: "rescuer_applications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_rescuer_profiles_approved_by",
                table: "rescuer_profiles",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_reusable_items_depot_id",
                table: "reusable_items",
                column: "depot_id");

            migrationBuilder.CreateIndex(
                name: "IX_reusable_items_item_model_id",
                table: "reusable_items",
                column: "item_model_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_claim_id",
                table: "role_permissions",
                column: "claim_id");

            migrationBuilder.CreateIndex(
                name: "roles_name_key",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sos_ai_analysis_sos_request_id",
                table: "sos_ai_analysis",
                column: "sos_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_sos_request_companions_request_user",
                table: "sos_request_companions",
                columns: new[] { "sos_request_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sos_request_companions_user_id",
                table: "sos_request_companions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_sos_request_updates_sos_request_id",
                table: "sos_request_updates",
                column: "sos_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_sos_requests_cluster_id",
                table: "sos_requests",
                column: "cluster_id");

            migrationBuilder.CreateIndex(
                name: "IX_sos_requests_reviewed_by",
                table: "sos_requests",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_sos_requests_user_id",
                table: "sos_requests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_sos_rule_evaluations_sos_request_id",
                table: "sos_rule_evaluations",
                column: "sos_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_supply_inventory_depot_id",
                table: "supply_inventory",
                column: "depot_id");

            migrationBuilder.CreateIndex(
                name: "IX_supply_inventory_item_model_id",
                table: "supply_inventory",
                column: "item_model_id");

            migrationBuilder.CreateIndex(
                name: "ix_supply_inventory_lots_fefo",
                table: "supply_inventory_lots",
                columns: new[] { "supply_inventory_id", "remaining_quantity", "expired_date" });

            migrationBuilder.CreateIndex(
                name: "ix_system_fund_transactions_fund_id",
                table: "system_fund_transactions",
                column: "system_fund_id");

            migrationBuilder.CreateIndex(
                name: "target_groups_name_key",
                table: "target_groups",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_team_incident_activities_mission_activity_id",
                table: "team_incident_activities",
                column: "mission_activity_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_incidents_mission_activity_id",
                table: "team_incidents",
                column: "mission_activity_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_incidents_mission_team_id",
                table: "team_incidents",
                column: "mission_team_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_incidents_support_sos_request_id",
                table: "team_incidents",
                column: "support_sos_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_abilities_ability_id",
                table: "user_abilities",
                column: "ability_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_notification_id",
                table: "user_notifications",
                column: "notification_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_user_id",
                table: "user_notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_permissions_claim_id",
                table: "user_permissions",
                column: "claim_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_relative_profiles_user_id",
                table: "user_relative_profiles",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_relative_profiles_user_id_profile_updated_at",
                table: "user_relative_profiles",
                columns: new[] { "user_id", "profile_updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_users_assembly_point_id",
                table: "users",
                column: "assembly_point_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_role_id",
                table: "users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "users_username_key",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vat_invoice_items_item_model_id",
                table: "vat_invoice_items",
                column: "item_model_id");

            migrationBuilder.CreateIndex(
                name: "IX_vat_invoice_items_vat_invoice_id",
                table: "vat_invoice_items",
                column: "vat_invoice_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_ai_suggestions");

            migrationBuilder.DropTable(
                name: "ai_configs");

            migrationBuilder.DropTable(
                name: "assembly_participants");

            migrationBuilder.DropTable(
                name: "assembly_point_check_in_radius_configs");

            migrationBuilder.DropTable(
                name: "check_in_radius_configs");

            migrationBuilder.DropTable(
                name: "cluster_ai_analysis");

            migrationBuilder.DropTable(
                name: "conversation_participants");

            migrationBuilder.DropTable(
                name: "depot_closure_external_items");

            migrationBuilder.DropTable(
                name: "depot_closure_transfer_consumable_reservations");

            migrationBuilder.DropTable(
                name: "depot_closure_transfer_items");

            migrationBuilder.DropTable(
                name: "depot_closure_transfer_reusable_items");

            migrationBuilder.DropTable(
                name: "depot_fund_transactions");

            migrationBuilder.DropTable(
                name: "depot_managers");

            migrationBuilder.DropTable(
                name: "depot_realtime_outbox");

            migrationBuilder.DropTable(
                name: "depot_supply_request_consumable_reservations");

            migrationBuilder.DropTable(
                name: "depot_supply_request_items");

            migrationBuilder.DropTable(
                name: "depot_supply_request_reusable_items");

            migrationBuilder.DropTable(
                name: "disbursement_items");

            migrationBuilder.DropTable(
                name: "donations");

            migrationBuilder.DropTable(
                name: "fund_transactions");

            migrationBuilder.DropTable(
                name: "funding_request_items");

            migrationBuilder.DropTable(
                name: "inventory_logs");

            migrationBuilder.DropTable(
                name: "inventory_stock_threshold_config_history");

            migrationBuilder.DropTable(
                name: "item_model_target_groups");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "mission_activity_reports");

            migrationBuilder.DropTable(
                name: "mission_activity_sync_mutations");

            migrationBuilder.DropTable(
                name: "mission_ai_suggestions");

            migrationBuilder.DropTable(
                name: "mission_items");

            migrationBuilder.DropTable(
                name: "mission_team_member_evaluations");

            migrationBuilder.DropTable(
                name: "mission_team_members");

            migrationBuilder.DropTable(
                name: "organization_relief_items");

            migrationBuilder.DropTable(
                name: "prompts");

            migrationBuilder.DropTable(
                name: "rescue_team_ai_suggestions");

            migrationBuilder.DropTable(
                name: "rescue_team_members");

            migrationBuilder.DropTable(
                name: "rescue_team_radius_configs");

            migrationBuilder.DropTable(
                name: "rescuer_application_documents");

            migrationBuilder.DropTable(
                name: "rescuer_score_visibility_configs");

            migrationBuilder.DropTable(
                name: "rescuer_scores");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "service_zones");

            migrationBuilder.DropTable(
                name: "sos_ai_analysis");

            migrationBuilder.DropTable(
                name: "sos_cluster_grouping_configs");

            migrationBuilder.DropTable(
                name: "sos_priority_rule_configs");

            migrationBuilder.DropTable(
                name: "sos_request_companions");

            migrationBuilder.DropTable(
                name: "sos_request_updates");

            migrationBuilder.DropTable(
                name: "sos_rule_evaluations");

            migrationBuilder.DropTable(
                name: "stock_warning_band_config");

            migrationBuilder.DropTable(
                name: "supply_request_priority_configs");

            migrationBuilder.DropTable(
                name: "system_fund_transactions");

            migrationBuilder.DropTable(
                name: "system_migration_audit");

            migrationBuilder.DropTable(
                name: "team_incident_activities");

            migrationBuilder.DropTable(
                name: "user_abilities");

            migrationBuilder.DropTable(
                name: "user_notifications");

            migrationBuilder.DropTable(
                name: "user_permissions");

            migrationBuilder.DropTable(
                name: "user_relative_profiles");

            migrationBuilder.DropTable(
                name: "vat_invoice_items");

            migrationBuilder.DropTable(
                name: "assembly_events");

            migrationBuilder.DropTable(
                name: "depot_closure_transfers");

            migrationBuilder.DropTable(
                name: "depot_funds");

            migrationBuilder.DropTable(
                name: "depot_supply_requests");

            migrationBuilder.DropTable(
                name: "campaign_disbursements");

            migrationBuilder.DropTable(
                name: "reusable_items");

            migrationBuilder.DropTable(
                name: "supply_inventory_lots");

            migrationBuilder.DropTable(
                name: "inventory_stock_threshold_configs");

            migrationBuilder.DropTable(
                name: "target_groups");

            migrationBuilder.DropTable(
                name: "conversations");

            migrationBuilder.DropTable(
                name: "mission_team_reports");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropTable(
                name: "document_file_types");

            migrationBuilder.DropTable(
                name: "rescuer_applications");

            migrationBuilder.DropTable(
                name: "rescuer_profiles");

            migrationBuilder.DropTable(
                name: "system_funds");

            migrationBuilder.DropTable(
                name: "team_incidents");

            migrationBuilder.DropTable(
                name: "abilities");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "vat_invoices");

            migrationBuilder.DropTable(
                name: "depot_closures");

            migrationBuilder.DropTable(
                name: "funding_requests");

            migrationBuilder.DropTable(
                name: "supply_inventory");

            migrationBuilder.DropTable(
                name: "document_file_type_categories");

            migrationBuilder.DropTable(
                name: "mission_activities");

            migrationBuilder.DropTable(
                name: "sos_requests");

            migrationBuilder.DropTable(
                name: "ability_subgroups");

            migrationBuilder.DropTable(
                name: "fund_campaigns");

            migrationBuilder.DropTable(
                name: "depots");

            migrationBuilder.DropTable(
                name: "item_models");

            migrationBuilder.DropTable(
                name: "mission_teams");

            migrationBuilder.DropTable(
                name: "ability_categories");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "missions");

            migrationBuilder.DropTable(
                name: "rescue_teams");

            migrationBuilder.DropTable(
                name: "sos_clusters");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "assembly_points");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropSequence(
                name: "depot_realtime_version_seq");
        }
    }
}
