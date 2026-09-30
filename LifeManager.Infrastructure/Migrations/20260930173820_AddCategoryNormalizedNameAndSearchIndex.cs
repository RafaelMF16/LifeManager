using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryNormalizedNameAndSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Categories_UserId_Name",
                table: "Categories");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Categories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // Backfill existing rows with the same normalization the domain applies (SearchText.Normalize:
            // trim + lowercase + strip accents). unaccent is only needed for this one-off UPDATE; new rows
            // get NormalizedName from the application. Must run before the unique index below.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
            migrationBuilder.Sql("""UPDATE "Categories" SET "NormalizedName" = lower(unaccent(btrim("Name")));""");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_NormalizedName",
                table: "Categories",
                column: "NormalizedName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UserId_NormalizedName",
                table: "Categories",
                columns: new[] { "UserId", "NormalizedName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Categories_NormalizedName",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_UserId_NormalizedName",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Categories");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UserId_Name",
                table: "Categories",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }
    }
}
