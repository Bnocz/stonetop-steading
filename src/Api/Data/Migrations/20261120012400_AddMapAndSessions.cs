using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMapAndSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MapImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SteadingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    Version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapImages_Steadings_SteadingId",
                        column: x => x.SteadingId,
                        principalTable: "Steadings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaySessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SteadingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GmOnly = table.Column<bool>(type: "boolean", nullable: false),
                    Waypoints = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaySessions", x => x.Id);
                    table.CheckConstraint("CK_PlaySessions_Number", "\"Number\" BETWEEN 1 AND 999");
                    table.ForeignKey(
                        name: "FK_PlaySessions_Steadings_SteadingId",
                        column: x => x.SteadingId,
                        principalTable: "Steadings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MapImages_SteadingId",
                table: "MapImages",
                column: "SteadingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_SteadingId_Number",
                table: "PlaySessions",
                columns: new[] { "SteadingId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MapImages");

            migrationBuilder.DropTable(
                name: "PlaySessions");
        }
    }
}
