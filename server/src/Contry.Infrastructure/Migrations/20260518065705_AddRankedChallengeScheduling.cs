using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRankedChallengeScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomClueDataJson",
                table: "ranked_challenges",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "ranked_challenges",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomClueDataJson",
                table: "ranked_challenges");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "ranked_challenges");
        }
    }
}
