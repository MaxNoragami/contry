using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCluePacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clue_packs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetId = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Comparator = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UnitSymbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CategoriesJson = table.Column<string>(type: "jsonb", nullable: true),
                    RowsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clue_packs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clue_packs_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_clue_packs_OwnerId",
                table: "clue_packs",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_clue_packs_OwnerId_DatasetId",
                table: "clue_packs",
                columns: new[] { "OwnerId", "DatasetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clue_packs_UpdatedAtUtc",
                table: "clue_packs",
                column: "UpdatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clue_packs");
        }
    }
}
