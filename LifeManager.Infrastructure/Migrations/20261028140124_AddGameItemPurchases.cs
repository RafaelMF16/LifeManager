using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameItemPurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ItemKind",
                table: "GameLedgerEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GameItemPurchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    CostPaid = table.Column<int>(type: "integer", nullable: false),
                    HpRestored = table.Column<int>(type: "integer", nullable: false),
                    PurchasedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PurchasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UndoneAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameItemPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameItemPurchases_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameItemPurchases_UserId_PurchasedOn",
                table: "GameItemPurchases",
                columns: new[] { "UserId", "PurchasedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameItemPurchases");

            migrationBuilder.DropColumn(
                name: "ItemKind",
                table: "GameLedgerEntries");
        }
    }
}
