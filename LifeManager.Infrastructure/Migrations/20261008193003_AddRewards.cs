using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RewardId",
                table: "GameLedgerEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Rewards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Cost = table.Column<int>(type: "integer", nullable: false),
                    Icon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rewards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rewards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RewardRedemptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RewardId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    RewardName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    RewardIcon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CostPaid = table.Column<int>(type: "integer", nullable: false),
                    RedeemedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    RedeemedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UndoneAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardRedemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewardRedemptions_Rewards_RewardId",
                        column: x => x.RewardId,
                        principalTable: "Rewards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RewardRedemptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameLedgerEntries_RewardId",
                table: "GameLedgerEntries",
                column: "RewardId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_RewardId",
                table: "RewardRedemptions",
                column: "RewardId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_UserId_RedeemedAt_Id",
                table: "RewardRedemptions",
                columns: new[] { "UserId", "RedeemedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_NormalizedName",
                table: "Rewards",
                column: "NormalizedName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_UserId_ArchivedAt",
                table: "Rewards",
                columns: new[] { "UserId", "ArchivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_UserId_NormalizedName",
                table: "Rewards",
                columns: new[] { "UserId", "NormalizedName" },
                unique: true,
                filter: "\"ArchivedAt\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_GameLedgerEntries_Rewards_RewardId",
                table: "GameLedgerEntries",
                column: "RewardId",
                principalTable: "Rewards",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameLedgerEntries_Rewards_RewardId",
                table: "GameLedgerEntries");

            migrationBuilder.DropTable(
                name: "RewardRedemptions");

            migrationBuilder.DropTable(
                name: "Rewards");

            migrationBuilder.DropIndex(
                name: "IX_GameLedgerEntries_RewardId",
                table: "GameLedgerEntries");

            migrationBuilder.DropColumn(
                name: "RewardId",
                table: "GameLedgerEntries");
        }
    }
}
