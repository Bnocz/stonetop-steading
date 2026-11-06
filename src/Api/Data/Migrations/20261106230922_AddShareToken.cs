using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShareToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShareToken",
                table: "Steadings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Existing steadings need their own secure random token before the column can be required.
            // gen_random_uuid() is cryptographically random and built into Postgres 13+.
            migrationBuilder.Sql(
                """
                UPDATE "Steadings"
                SET "ShareToken" = replace(gen_random_uuid()::text, '-', '') || replace(gen_random_uuid()::text, '-', '')
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ShareToken",
                table: "Steadings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Steadings_ShareToken",
                table: "Steadings",
                column: "ShareToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Steadings_ShareToken",
                table: "Steadings");

            migrationBuilder.DropColumn(
                name: "ShareToken",
                table: "Steadings");
        }
    }
}
