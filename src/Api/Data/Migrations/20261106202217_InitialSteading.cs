using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSteading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Steadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Size = table.Column<int>(type: "integer", nullable: false),
                    Fortunes = table.Column<int>(type: "integer", nullable: false),
                    Surplus = table.Column<int>(type: "integer", nullable: false),
                    Prosperity = table.Column<int>(type: "integer", nullable: false),
                    Defenses = table.Column<int>(type: "integer", nullable: false),
                    Population = table.Column<int>(type: "integer", nullable: false),
                    Diminished = table.Column<bool>(type: "boolean", nullable: false),
                    Lacking = table.Column<bool>(type: "boolean", nullable: false),
                    Malcontent = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Steadings", x => x.Id);
                    table.CheckConstraint("CK_Steadings_Defenses", "\"Defenses\" >= -2 AND \"Defenses\" <= 3");
                    table.CheckConstraint("CK_Steadings_Fortunes", "\"Fortunes\" >= -3 AND \"Fortunes\" <= 3");
                    table.CheckConstraint("CK_Steadings_Population", "\"Population\" >= -2 AND \"Population\" <= 2");
                    table.CheckConstraint("CK_Steadings_Prosperity", "\"Prosperity\" >= -2 AND \"Prosperity\" <= 2");
                    table.CheckConstraint("CK_Steadings_Size", "\"Size\" >= -1 AND \"Size\" <= 2");
                    table.CheckConstraint("CK_Steadings_Surplus", "\"Surplus\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "LogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SteadingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OldValue = table.Column<int>(type: "integer", nullable: false),
                    NewValue = table.Column<int>(type: "integer", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogEntries_Steadings_SteadingId",
                        column: x => x.SteadingId,
                        principalTable: "Steadings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogEntries_SteadingId_ChangedAt",
                table: "LogEntries",
                columns: new[] { "SteadingId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogEntries");

            migrationBuilder.DropTable(
                name: "Steadings");
        }
    }
}
