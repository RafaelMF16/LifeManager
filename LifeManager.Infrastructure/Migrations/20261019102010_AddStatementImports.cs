using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatementImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Transactions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatementImportId",
                table: "Transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CategorizationRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    MerchantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    Hits = table.Column<int>(type: "integer", nullable: false),
                    LastUsedOn = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategorizationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategorizationRules_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategorizationRules_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StatementImports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    FirstDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LastDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImportedCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedMonthCount = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatementImports_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StatementImportRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StatementImportId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    OriginalDescription = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MerchantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedDescription = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    SignedAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    Included = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DuplicateOfTransactionId = table.Column<int>(type: "integer", nullable: true),
                    DuplicateOfMonthlySummaryId = table.Column<int>(type: "integer", nullable: true),
                    SuggestionSource = table.Column<int>(type: "integer", nullable: false),
                    SuggestionConfidence = table.Column<int>(type: "integer", nullable: true),
                    SuggestionUses = table.Column<int>(type: "integer", nullable: false),
                    EditedByUser = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementImportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatementImportRows_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StatementImportRows_StatementImports_StatementImportId",
                        column: x => x.StatementImportId,
                        principalTable: "StatementImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_MonthlySummaryId_ExternalId",
                table: "Transactions",
                columns: new[] { "MonthlySummaryId", "ExternalId" },
                unique: true,
                filter: "\"ExternalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_StatementImportId",
                table: "Transactions",
                column: "StatementImportId");

            migrationBuilder.CreateIndex(
                name: "IX_CategorizationRules_CategoryId",
                table: "CategorizationRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CategorizationRules_UserId_MerchantKey_Type_Action_Category~",
                table: "CategorizationRules",
                columns: new[] { "UserId", "MerchantKey", "Type", "Action", "CategoryId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_StatementImportRows_CategoryId",
                table: "StatementImportRows",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StatementImportRows_StatementImportId_Date",
                table: "StatementImportRows",
                columns: new[] { "StatementImportId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_StatementImportRows_StatementImportId_MerchantKey",
                table: "StatementImportRows",
                columns: new[] { "StatementImportId", "MerchantKey" });

            migrationBuilder.CreateIndex(
                name: "IX_StatementImports_UserId_Status_CreatedAt",
                table: "StatementImports",
                columns: new[] { "UserId", "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_StatementImports_StatementImportId",
                table: "Transactions",
                column: "StatementImportId",
                principalTable: "StatementImports",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_StatementImports_StatementImportId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "CategorizationRules");

            migrationBuilder.DropTable(
                name: "StatementImportRows");

            migrationBuilder.DropTable(
                name: "StatementImports");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_MonthlySummaryId_ExternalId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_StatementImportId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "StatementImportId",
                table: "Transactions");
        }
    }
}
