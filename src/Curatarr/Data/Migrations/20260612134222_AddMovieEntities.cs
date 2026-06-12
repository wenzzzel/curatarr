using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Curatarr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMovieEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Movies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RadarrId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovieFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: false),
                    Side = table.Column<int>(type: "INTEGER", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Quality = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ObservedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovieFiles_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovieSubtitleFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: false),
                    Side = table.Column<int>(type: "INTEGER", nullable: false),
                    Suffix = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieSubtitleFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovieSubtitleFiles_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrphanedMovieFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrphanedMovieFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrphanedMovieFiles_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovieFiles_MovieId_Side",
                table: "MovieFiles",
                columns: new[] { "MovieId", "Side" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movies_RadarrId",
                table: "Movies",
                column: "RadarrId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovieSubtitleFiles_MovieId_Side_Suffix",
                table: "MovieSubtitleFiles",
                columns: new[] { "MovieId", "Side", "Suffix" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrphanedMovieFiles_MovieId_RelativePath",
                table: "OrphanedMovieFiles",
                columns: new[] { "MovieId", "RelativePath" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovieFiles");

            migrationBuilder.DropTable(
                name: "MovieSubtitleFiles");

            migrationBuilder.DropTable(
                name: "OrphanedMovieFiles");

            migrationBuilder.DropTable(
                name: "Movies");
        }
    }
}
