using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaroFlowEngine.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerBillingAndSegment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "billing_due_day",
                table: "customers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "segment",
                table: "customers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_customers_billing_due_day",
                table: "customers",
                sql: "billing_due_day IS NULL OR billing_due_day BETWEEN 1 AND 28");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_customers_billing_due_day",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "billing_due_day",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "segment",
                table: "customers");
        }
    }
}
