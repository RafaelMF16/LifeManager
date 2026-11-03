using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHabitAutoTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutoTrigger",
                table: "Habits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Habits_UserId_AutoTrigger",
                table: "Habits",
                columns: new[] { "UserId", "AutoTrigger" },
                filter: "\"AutoTrigger\" <> 0 AND \"ArchivedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Habits_UserId_AutoTrigger",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "AutoTrigger",
                table: "Habits");
        }
    }
}
