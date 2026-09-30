using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceFoodTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStatementContentHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_statements_user_id",
                table: "statements");

            migrationBuilder.AddColumn<string>(
                name: "content_hash",
                table: "statements",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_statements_user_id_content_hash",
                table: "statements",
                columns: new[] { "user_id", "content_hash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_statements_user_id_content_hash",
                table: "statements");

            migrationBuilder.DropColumn(
                name: "content_hash",
                table: "statements");

            migrationBuilder.CreateIndex(
                name: "IX_statements_user_id",
                table: "statements",
                column: "user_id");
        }
    }
}
