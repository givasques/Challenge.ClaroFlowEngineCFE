using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaroFlowEngine.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAiSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_ai_summaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    input_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    content = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_ai_summaries", x => x.id);
                    table.CheckConstraint("ck_customer_ai_summaries_source", "source IN ('ai', 'rules')");
                    table.ForeignKey(
                        name: "fk_customer_ai_summaries_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_ai_summaries_customer_fingerprint",
                table: "customer_ai_summaries",
                columns: new[] { "customer_id", "input_fingerprint" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_ai_summaries_customer_id",
                table: "customer_ai_summaries",
                column: "customer_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_ai_summaries");
        }
    }
}
