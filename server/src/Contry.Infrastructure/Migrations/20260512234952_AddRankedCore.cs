using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRankedCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ranked_challenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeDateUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    TargetCountryId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ClueSetJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ranked_challenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ranked_user_stats",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayedCount = table.Column<int>(type: "integer", nullable: false),
                    WonCount = table.Column<int>(type: "integer", nullable: false),
                    TotalGuessesOnWins = table.Column<int>(type: "integer", nullable: false),
                    FastestWinGuessCount = table.Column<int>(type: "integer", nullable: true),
                    SlowestWinGuessCount = table.Column<int>(type: "integer", nullable: true),
                    CurrentStreak = table.Column<int>(type: "integer", nullable: false),
                    BestStreak = table.Column<int>(type: "integer", nullable: false),
                    LastCompletedChallengeDateUtc = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ranked_user_stats", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_ranked_user_stats_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ranked_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RankedChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    GuessCount = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ranked_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ranked_sessions_ranked_challenges_RankedChallengeId",
                        column: x => x.RankedChallengeId,
                        principalTable: "ranked_challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ranked_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ranked_guesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RankedSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    GuessCountryId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    GuessCountryName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ResultsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ranked_guesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ranked_guesses_ranked_sessions_RankedSessionId",
                        column: x => x.RankedSessionId,
                        principalTable: "ranked_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ranked_challenges_ChallengeDateUtc",
                table: "ranked_challenges",
                column: "ChallengeDateUtc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ranked_guesses_RankedSessionId_AttemptNumber",
                table: "ranked_guesses",
                columns: new[] { "RankedSessionId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ranked_guesses_RankedSessionId_GuessCountryId",
                table: "ranked_guesses",
                columns: new[] { "RankedSessionId", "GuessCountryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ranked_sessions_RankedChallengeId",
                table: "ranked_sessions",
                column: "RankedChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_ranked_sessions_UserId_RankedChallengeId",
                table: "ranked_sessions",
                columns: new[] { "UserId", "RankedChallengeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ranked_guesses");

            migrationBuilder.DropTable(
                name: "ranked_user_stats");

            migrationBuilder.DropTable(
                name: "ranked_sessions");

            migrationBuilder.DropTable(
                name: "ranked_challenges");
        }
    }
}
