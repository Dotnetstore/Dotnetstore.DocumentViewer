using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentAllowedIps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentAllowedIps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cidr = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AddedById = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAllowedIps", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAllowedIps_DocumentId",
                table: "DocumentAllowedIps",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAllowedIps_DocumentId_Cidr",
                table: "DocumentAllowedIps",
                columns: new[] { "DocumentId", "Cidr" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentAllowedIps");
        }
    }
}
