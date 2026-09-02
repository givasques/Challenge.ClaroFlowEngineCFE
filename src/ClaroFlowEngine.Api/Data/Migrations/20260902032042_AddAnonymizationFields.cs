using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaroFlowEngine.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnonymizationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_customers_cpf_format",
                table: "customers");

            migrationBuilder.AlterColumn<Guid>(
                name: "journey_context_id",
                table: "journey_transitions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "cpf",
                table: "customers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(11)",
                oldMaxLength: 11);

            migrationBuilder.AddColumn<string>(
                name: "anonymization_source",
                table: "customers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "anonymized_at",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_customers_cpf_format",
                table: "customers",
                sql: "cpf ~ '^\\d{11}$' OR cpf ~ '^[0-9a-f]{64}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_customers_cpf_format",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "anonymization_source",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "anonymized_at",
                table: "customers");

            migrationBuilder.AlterColumn<Guid>(
                name: "journey_context_id",
                table: "journey_transitions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "cpf",
                table: "customers",
                type: "character varying(11)",
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AddCheckConstraint(
                name: "ck_customers_cpf_format",
                table: "customers",
                sql: "cpf ~ '^\\d{11}$'");
        }
    }
}
