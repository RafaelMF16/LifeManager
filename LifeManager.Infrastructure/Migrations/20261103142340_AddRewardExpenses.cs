using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExpenseAmount",
                table: "Rewards",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpenseCategoryId",
                table: "Rewards",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_ExpenseCategoryId",
                table: "Rewards",
                column: "ExpenseCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Rewards_Categories_ExpenseCategoryId",
                table: "Rewards",
                column: "ExpenseCategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rewards_Categories_ExpenseCategoryId",
                table: "Rewards");

            migrationBuilder.DropIndex(
                name: "IX_Rewards_ExpenseCategoryId",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "ExpenseAmount",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "ExpenseCategoryId",
                table: "Rewards");
        }
    }
}
