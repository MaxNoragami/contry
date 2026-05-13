using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalDatasetCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "built_in_dataset_documents",
                columns: table => new
                {
                    Path = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Checksum = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_built_in_dataset_documents", x => x.Path);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "built_in_dataset_documents");
        }
    }
}
