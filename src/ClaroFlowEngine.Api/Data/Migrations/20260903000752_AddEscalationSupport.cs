using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaroFlowEngine.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEscalationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_journey_status",
                table: "journey_contexts");

            migrationBuilder.AddColumn<DateTime>(
                name: "escalated_at",
                table: "journey_contexts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_journey_status_open_escalated",
                table: "journey_contexts",
                column: "status",
                filter: "status IN ('open', 'escalated')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_journey_status",
                table: "journey_contexts",
                sql: "status IN ('open', 'concluded', 'expired', 'abandoned', 'escalated')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_journey_status_open_escalated",
                table: "journey_contexts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_journey_status",
                table: "journey_contexts");

            migrationBuilder.DropColumn(
                name: "escalated_at",
                table: "journey_contexts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_journey_status",
                table: "journey_contexts",
                sql: "status IN ('open', 'concluded', 'expired', 'abandoned')");
        }
    }
}
