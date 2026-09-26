using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceFoodTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedDishes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saved_dishes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    calories_min = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    calories_max = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    protein_min_g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    protein_max_g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    fat_min_g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    fat_max_g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    carbs_min_g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    carbs_max_g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    is_favorite = table.Column<bool>(type: "boolean", nullable: false),
                    use_count = table.Column<int>(type: "integer", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_dishes", x => x.id);
                    table.ForeignKey(
                        name: "FK_saved_dishes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_saved_dishes_user_id_name_key",
                table: "saved_dishes",
                columns: new[] { "user_id", "name_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_dishes");
        }
    }
}
