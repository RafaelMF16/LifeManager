using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailCooldowns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailCooldowns",
                columns: table => new
                {
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    LastSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailCooldowns", x => new { x.Email, x.Kind });
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailCooldowns_LastSentAt",
                table: "EmailCooldowns",
                column: "LastSentAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailCooldowns");
        }
    }
}
