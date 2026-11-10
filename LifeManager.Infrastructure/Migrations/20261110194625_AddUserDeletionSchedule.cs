using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDeletionSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletionRequestedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletionScheduledFor",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_DeletionScheduledFor",
                table: "Users",
                column: "DeletionScheduledFor",
                filter: "\"DeletionScheduledFor\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_DeletionScheduledFor",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletionRequestedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletionScheduledFor",
                table: "Users");
        }
    }
}
