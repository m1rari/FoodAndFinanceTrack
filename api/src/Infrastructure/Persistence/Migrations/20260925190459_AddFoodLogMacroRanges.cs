using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceFoodTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodLogMacroRanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "carbs_max_g",
                table: "food_logs",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "carbs_min_g",
                table: "food_logs",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fat_max_g",
                table: "food_logs",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fat_min_g",
                table: "food_logs",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "protein_max_g",
                table: "food_logs",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "protein_min_g",
                table: "food_logs",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "carbs_max_g",
                table: "food_logs");

            migrationBuilder.DropColumn(
                name: "carbs_min_g",
                table: "food_logs");

            migrationBuilder.DropColumn(
                name: "fat_max_g",
                table: "food_logs");

            migrationBuilder.DropColumn(
                name: "fat_min_g",
                table: "food_logs");

            migrationBuilder.DropColumn(
                name: "protein_max_g",
                table: "food_logs");

            migrationBuilder.DropColumn(
                name: "protein_min_g",
                table: "food_logs");
        }
    }
}
