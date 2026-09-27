using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceFoodTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodPortionAndMealGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "meal_group_id",
                table: "food_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "portion_grams",
                table: "food_logs",
                type: "numeric(8,1)",
                precision: 8,
                scale: 1,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "meal_group_id",
                table: "food_logs");

            migrationBuilder.DropColumn(
                name: "portion_grams",
                table: "food_logs");
        }
    }
}
