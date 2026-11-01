using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LifeManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRankingReactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RankingReactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    FromMemberId = table.Column<int>(type: "integer", nullable: false),
                    ToMemberId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingReactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RankingReactions_RankingGroupMembers_FromMemberId",
                        column: x => x.FromMemberId,
                        principalTable: "RankingGroupMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RankingReactions_RankingGroupMembers_ToMemberId",
                        column: x => x.ToMemberId,
                        principalTable: "RankingGroupMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RankingReactions_RankingGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "RankingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RankingReactions_FromMemberId",
                table: "RankingReactions",
                column: "FromMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_RankingReactions_GroupId_WeekStart_FromMemberId_ToMemberId",
                table: "RankingReactions",
                columns: new[] { "GroupId", "WeekStart", "FromMemberId", "ToMemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RankingReactions_ToMemberId",
                table: "RankingReactions",
                column: "ToMemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RankingReactions");
        }
    }
}
