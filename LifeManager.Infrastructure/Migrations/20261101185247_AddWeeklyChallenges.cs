using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChallengeId",
                table: "GameLedgerEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WeeklyChallenges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    HabitId = table.Column<int>(type: "integer", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Target = table.Column<int>(type: "integer", nullable: false),
                    Baseline = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Effort = table.Column<int>(type: "integer", nullable: false),
                    RewardCoins = table.Column<int>(type: "integer", nullable: false),
                    RewardXp = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeeklyChallenges_Habits_HabitId",
                        column: x => x.HabitId,
                        principalTable: "Habits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WeeklyChallenges_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameLedgerEntries_ChallengeId",
                table: "GameLedgerEntries",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyChallenges_HabitId_WeekStart",
                table: "WeeklyChallenges",
                columns: new[] { "HabitId", "WeekStart" },
                unique: true,
                filter: "\"Status\" <> 4");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyChallenges_UserId_WeekStart",
                table: "WeeklyChallenges",
                columns: new[] { "UserId", "WeekStart" });

            migrationBuilder.AddForeignKey(
                name: "FK_GameLedgerEntries_WeeklyChallenges_ChallengeId",
                table: "GameLedgerEntries",
                column: "ChallengeId",
                principalTable: "WeeklyChallenges",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameLedgerEntries_WeeklyChallenges_ChallengeId",
                table: "GameLedgerEntries");

            migrationBuilder.DropTable(
                name: "WeeklyChallenges");

            migrationBuilder.DropIndex(
                name: "IX_GameLedgerEntries_ChallengeId",
                table: "GameLedgerEntries");

            migrationBuilder.DropColumn(
                name: "ChallengeId",
                table: "GameLedgerEntries");
        }
    }
}
