using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardRedemptionTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TransactionId",
                table: "RewardRedemptions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_TransactionId",
                table: "RewardRedemptions",
                column: "TransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_RewardRedemptions_Transactions_TransactionId",
                table: "RewardRedemptions",
                column: "TransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RewardRedemptions_Transactions_TransactionId",
                table: "RewardRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_RewardRedemptions_TransactionId",
                table: "RewardRedemptions");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "RewardRedemptions");
        }
    }
}
