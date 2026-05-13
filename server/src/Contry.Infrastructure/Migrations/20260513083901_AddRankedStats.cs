using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRankedStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuessDistributionJson",
                table: "ranked_user_stats",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ranked_clue_usage_stats",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClueId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ranked_clue_usage_stats", x => new { x.UserId, x.ClueId });
                    table.ForeignKey(
                        name: "FK_ranked_clue_usage_stats_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ranked_country_discovery_stats",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Discovered = table.Column<bool>(type: "boolean", nullable: false),
                    BestAttempts = table.Column<int>(type: "integer", nullable: true),
                    SolvedCount = table.Column<int>(type: "integer", nullable: false),
                    LastSolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ranked_country_discovery_stats", x => new { x.UserId, x.CountryId });
                    table.ForeignKey(
                        name: "FK_ranked_country_discovery_stats_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ranked_clue_usage_stats");

            migrationBuilder.DropTable(
                name: "ranked_country_discovery_stats");

            migrationBuilder.DropColumn(
                name: "GuessDistributionJson",
                table: "ranked_user_stats");
        }
    }
}
