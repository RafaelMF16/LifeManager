using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetPlanTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "BudgetPlans",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "BudgetPlans",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Plans saved before this change count as made at the start of their month (midnight in São Paulo), so the
            // Finance achievements treat the existing history as eligible. A plan the previous revision saves while
            // the deploy runs keeps the column default (year 1), which is eligible too.
            migrationBuilder.Sql("""
                UPDATE "BudgetPlans"
                SET "CreatedAt" = ("MonthStart" + time '03:00') AT TIME ZONE 'UTC',
                    "UpdatedAt" = ("MonthStart" + time '03:00') AT TIME ZONE 'UTC';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "BudgetPlans");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "BudgetPlans");
        }
    }
}
