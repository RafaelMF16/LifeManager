using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceBudgetsWithBudgetPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Converts the versioned goals into one budget plan per user and month, from the user's first month with a
            // goal through the current month (America/Sao_Paulo); later months are left without a plan. Per month, with
            // the versions in force then (Type 1 = Expense, 3 = Investment):
            //   spending   = max(month-total expense goal, sum of category expense goals)
            //   investment = month-total investment goal, or else the sum of category investment goals
            //   total      = spending + investment
            // Each category expense goal becomes a slice of goal ÷ total (2 places), the investment one slice; the rest
            // is free. Slices rounding to 0% are dropped, and a rounding excess over 100% comes off the largest slice.
            migrationBuilder.Sql("""
                WITH bounds AS (
                    SELECT "UserId", MIN("EffectiveFrom") AS "FirstMonth"
                    FROM "Budgets"
                    GROUP BY "UserId"
                ),
                months AS (
                    SELECT bounds."UserId", month_start::date AS "Month"
                    FROM bounds,
                        generate_series(
                            bounds."FirstMonth",
                            date_trunc('month', now() AT TIME ZONE 'America/Sao_Paulo')::date,
                            interval '1 month') AS month_start
                ),
                goals AS (
                    SELECT months."UserId", months."Month", budget."Type", budget."CategoryId", budget."Amount"
                    FROM months
                    JOIN "Budgets" budget
                        ON budget."UserId" = months."UserId"
                        AND budget."EffectiveFrom" <= months."Month"
                        AND (budget."EffectiveTo" IS NULL OR budget."EffectiveTo" >= months."Month")
                ),
                per_month AS (
                    SELECT "UserId", "Month",
                        GREATEST(
                            COALESCE(MAX("Amount") FILTER (WHERE "Type" = 1 AND "CategoryId" IS NULL), 0),
                            COALESCE(SUM("Amount") FILTER (WHERE "Type" = 1 AND "CategoryId" IS NOT NULL), 0)) AS "Spending",
                        COALESCE(
                            MAX("Amount") FILTER (WHERE "Type" = 3 AND "CategoryId" IS NULL),
                            SUM("Amount") FILTER (WHERE "Type" = 3 AND "CategoryId" IS NOT NULL),
                            0) AS "Investment"
                    FROM goals
                    GROUP BY "UserId", "Month"
                )
                INSERT INTO "BudgetPlans" ("UserId", "MonthStart", "Total")
                SELECT "UserId", "Month", "Spending" + "Investment"
                FROM per_month
                WHERE "Spending" + "Investment" > 0;
                """);

            migrationBuilder.Sql("""
                WITH goals AS (
                    SELECT budget_plan."Id" AS "PlanId", budget_plan."Total", budget."Type", budget."CategoryId", budget."Amount"
                    FROM "BudgetPlans" budget_plan
                    JOIN "Budgets" budget
                        ON budget."UserId" = budget_plan."UserId"
                        AND budget."EffectiveFrom" <= budget_plan."MonthStart"
                        AND (budget."EffectiveTo" IS NULL OR budget."EffectiveTo" >= budget_plan."MonthStart")
                ),
                amounts AS (
                    SELECT "PlanId", "Total", 1 AS "Type", "CategoryId", "Amount"
                    FROM goals
                    WHERE "Type" = 1 AND "CategoryId" IS NOT NULL
                    UNION ALL
                    SELECT "PlanId", "Total", 3, NULL,
                        COALESCE(
                            MAX("Amount") FILTER (WHERE "CategoryId" IS NULL),
                            SUM("Amount") FILTER (WHERE "CategoryId" IS NOT NULL))
                    FROM goals
                    WHERE "Type" = 3
                    GROUP BY "PlanId", "Total"
                ),
                percents AS (
                    SELECT "PlanId", "Type", "CategoryId", ROUND("Amount" * 100 / "Total", 2) AS "Percent"
                    FROM amounts
                ),
                ranked AS (
                    SELECT "PlanId", "Type", "CategoryId", "Percent",
                        SUM("Percent") OVER (PARTITION BY "PlanId") AS "PlanPercent",
                        ROW_NUMBER() OVER (PARTITION BY "PlanId" ORDER BY "Percent" DESC, "Type", "CategoryId") AS "Rank"
                    FROM percents
                    WHERE "Percent" > 0
                )
                INSERT INTO "BudgetPlanSlices" ("BudgetPlanId", "Type", "CategoryId", "Percent")
                SELECT "PlanId", "Type", "CategoryId",
                    CASE WHEN "Rank" = 1 AND "PlanPercent" > 100 THEN "Percent" - ("PlanPercent" - 100) ELSE "Percent" END
                FROM ranked;
                """);

            migrationBuilder.DropTable(
                name: "Budgets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreates the table empty: the goals are not rebuilt from the plans, which keep what was converted.
            migrationBuilder.CreateTable(
                name: "Budgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Budgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Budgets_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Budgets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_CategoryId",
                table: "Budgets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId_Type_CategoryId_EffectiveFrom",
                table: "Budgets",
                columns: new[] { "UserId", "Type", "CategoryId", "EffectiveFrom" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}
