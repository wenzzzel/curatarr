using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Curatarr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubtitleOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Origin",
                table: "SubtitleFiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Origin",
                table: "MovieSubtitleFiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Origin",
                table: "SubtitleFiles");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "MovieSubtitleFiles");
        }
    }
}
