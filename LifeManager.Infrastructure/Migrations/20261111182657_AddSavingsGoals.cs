using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSavingsGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SavingsGoalId",
                table: "Transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SavingsGoals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    TargetMonthStart = table.Column<DateOnly>(type: "date", nullable: true),
                    InitialAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Icon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavingsGoals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SavingsGoalId",
                table: "Transactions",
                column: "SavingsGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsGoals_NormalizedName",
                table: "SavingsGoals",
                column: "NormalizedName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_SavingsGoals_UserId_NormalizedName",
                table: "SavingsGoals",
                columns: new[] { "UserId", "NormalizedName" },
                unique: true,
                filter: "\"ArchivedAt\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_SavingsGoals_SavingsGoalId",
                table: "Transactions",
                column: "SavingsGoalId",
                principalTable: "SavingsGoals",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_SavingsGoals_SavingsGoalId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "SavingsGoals");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_SavingsGoalId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SavingsGoalId",
                table: "Transactions");
        }
    }
}
