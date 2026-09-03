using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaroFlowEngine.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunitiesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "opportunities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    urgency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "new"),
                    triggering_journey_id = table.Column<Guid>(type: "uuid", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    valid_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    contacted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    contacted_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opportunities", x => x.id);
                    table.CheckConstraint("ck_opportunities_status", "status IN ('new', 'contacted', 'converted', 'not_relevant')");
                    table.CheckConstraint("ck_opportunities_urgency", "urgency IN ('critical', 'high', 'medium', 'low')");
                    table.ForeignKey(
                        name: "fk_opportunities_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_opportunities_journey_contexts_triggering_journey_id",
                        column: x => x.triggering_journey_id,
                        principalTable: "journey_contexts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_opp_customer",
                table: "opportunities",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_opp_status_urgency",
                table: "opportunities",
                columns: new[] { "status", "urgency" },
                filter: "status IN ('new', 'contacted')");

            migrationBuilder.CreateIndex(
                name: "idx_opp_valid_until",
                table: "opportunities",
                column: "valid_until",
                filter: "status IN ('new', 'contacted')");

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_triggering_journey_id",
                table: "opportunities",
                column: "triggering_journey_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "opportunities");
        }
    }
}
