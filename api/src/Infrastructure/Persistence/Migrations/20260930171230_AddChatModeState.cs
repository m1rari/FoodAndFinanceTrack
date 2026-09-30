using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceFoodTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatModeState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chat_modes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_id = table.Column<long>(type: "bigint", nullable: false),
                    mode = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_modes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chat_modes_chat_id",
                table: "chat_modes",
                column: "chat_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_modes");
        }
    }
}
