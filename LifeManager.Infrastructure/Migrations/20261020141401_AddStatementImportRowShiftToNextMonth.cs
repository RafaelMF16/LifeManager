using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatementImportRowShiftToNextMonth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveDate",
                table: "StatementImportRows",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            // Drafts created before this column count in the month of their date.
            migrationBuilder.Sql("""UPDATE "StatementImportRows" SET "EffectiveDate" = "Date";""");

            migrationBuilder.AddColumn<bool>(
                name: "ShiftLearned",
                table: "StatementImportRows",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShiftToNextMonth",
                table: "StatementImportRows",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EffectiveDate",
                table: "StatementImportRows");

            migrationBuilder.DropColumn(
                name: "ShiftLearned",
                table: "StatementImportRows");

            migrationBuilder.DropColumn(
                name: "ShiftToNextMonth",
                table: "StatementImportRows");
        }
    }
}
