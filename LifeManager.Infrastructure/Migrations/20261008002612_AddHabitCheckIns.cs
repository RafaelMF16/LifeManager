using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHabitCheckIns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HabitId",
                table: "GameLedgerEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HabitCheckIns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HabitId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CoinsAwarded = table.Column<int>(type: "integer", nullable: false),
                    XpAwarded = table.Column<int>(type: "integer", nullable: false),
                    HpAwarded = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HabitCheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HabitCheckIns_Habits_HabitId",
                        column: x => x.HabitId,
                        principalTable: "Habits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HabitCheckIns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameLedgerEntries_HabitId",
                table: "GameLedgerEntries",
                column: "HabitId");

            migrationBuilder.CreateIndex(
                name: "IX_HabitCheckIns_HabitId_Date",
                table: "HabitCheckIns",
                columns: new[] { "HabitId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HabitCheckIns_UserId_Date",
                table: "HabitCheckIns",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.AddForeignKey(
                name: "FK_GameLedgerEntries_Habits_HabitId",
                table: "GameLedgerEntries",
                column: "HabitId",
                principalTable: "Habits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameLedgerEntries_Habits_HabitId",
                table: "GameLedgerEntries");

            migrationBuilder.DropTable(
                name: "HabitCheckIns");

            migrationBuilder.DropIndex(
                name: "IX_GameLedgerEntries_HabitId",
                table: "GameLedgerEntries");

            migrationBuilder.DropColumn(
                name: "HabitId",
                table: "GameLedgerEntries");
        }
    }
}
