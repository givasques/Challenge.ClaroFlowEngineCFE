using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaroFlowEngine.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,");

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                    table.CheckConstraint("ck_customers_cpf_format", "cpf ~ '^\\d{11}$'");
                });

            migrationBuilder.CreateTable(
                name: "plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    data_gb = table.Column<int>(type: "integer", nullable: false),
                    monthly_price_cents = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plans", x => x.id);
                    table.CheckConstraint("ck_plans_price_positive", "monthly_price_cents > 0");
                });

            migrationBuilder.CreateTable(
                name: "identity_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    identifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identity_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_identity_links_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "journey_contexts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    intent = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    current_step = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journey_contexts", x => x.id);
                    table.CheckConstraint("ck_journey_status", "status IN ('open', 'concluded', 'expired', 'abandoned')");
                    table.ForeignKey(
                        name: "fk_journey_contexts_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_plans", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_plans_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_customer_plans_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "handoff_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    journey_context_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_handoff_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_handoff_tokens_journey_contexts_journey_context_id",
                        column: x => x.journey_context_id,
                        principalTable: "journey_contexts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "journey_transitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    journey_context_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journey_transitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_journey_transitions_journey_contexts_journey_context_id",
                        column: x => x.journey_context_id,
                        principalTable: "journey_contexts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_plans_customer",
                table: "customer_plans",
                columns: new[] { "customer_id", "active" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_plans_plan_id",
                table: "customer_plans",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ux_customers_cpf",
                table: "customers",
                column: "cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_handoff_tokens_journey_context_id",
                table: "handoff_tokens",
                column: "journey_context_id");

            migrationBuilder.CreateIndex(
                name: "ix_handoff_tokens_token",
                table: "handoff_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_identity_links_customer",
                table: "identity_links",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ux_identity_links_channel_identifier",
                table: "identity_links",
                columns: new[] { "channel", "identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_journey_customer_status",
                table: "journey_contexts",
                columns: new[] { "customer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_journey_open_updated",
                table: "journey_contexts",
                column: "updated_at",
                filter: "status = 'open'");

            migrationBuilder.CreateIndex(
                name: "ix_transitions_journey_occurred",
                table: "journey_transitions",
                columns: new[] { "journey_context_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ux_plans_code",
                table: "plans",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_plans");

            migrationBuilder.DropTable(
                name: "handoff_tokens");

            migrationBuilder.DropTable(
                name: "identity_links");

            migrationBuilder.DropTable(
                name: "journey_transitions");

            migrationBuilder.DropTable(
                name: "plans");

            migrationBuilder.DropTable(
                name: "journey_contexts");

            migrationBuilder.DropTable(
                name: "customers");
        }
    }
}
