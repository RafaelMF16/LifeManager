using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHabitTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetStep",
                table: "Habits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetUnit",
                table: "Habits",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetValue",
                table: "Habits",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetStep",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "TargetUnit",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "TargetValue",
                table: "Habits");
        }
    }
}
