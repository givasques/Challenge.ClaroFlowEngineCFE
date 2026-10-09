using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaroFlowEngine.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHandoffOwnerMismatchAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "owner_mismatch_attempts",
                table: "handoff_tokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "owner_mismatch_attempts",
                table: "handoff_tokens");
        }
    }
}
