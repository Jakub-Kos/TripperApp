using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Adapters.Persistence.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddDateOptionsAndVotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DateOptions",
                columns: table => new
                {
                    DateOptionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TripId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DateIso = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DateOptions", x => x.DateOptionId);
                    table.ForeignKey(
                        name: "FK_DateOptions_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "TripId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DateVotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateOptionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DateVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DateVotes_DateOptions_DateOptionId",
                        column: x => x.DateOptionId,
                        principalTable: "DateOptions",
                        principalColumn: "DateOptionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DateOptions_TripId_DateIso",
                table: "DateOptions",
                columns: new[] { "TripId", "DateIso" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DateVotes_DateOptionId_UserId",
                table: "DateVotes",
                columns: new[] { "DateOptionId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DateVotes");

            migrationBuilder.DropTable(
                name: "DateOptions");
        }
    }
}
