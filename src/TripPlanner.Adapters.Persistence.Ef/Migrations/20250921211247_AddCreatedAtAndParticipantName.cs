using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Adapters.Persistence.Ef.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedAtAndParticipantName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Trips",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "DescriptionMarkdown",
                table: "Trips",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "TripParticipants",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "TripParticipants",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsAnonymous",
                table: "TripParticipants",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParticipantId",
                table: "TripParticipants",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_TripParticipants_UserId",
                table: "TripParticipants",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TripParticipants_Users_UserId",
                table: "TripParticipants",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripParticipants_Users_UserId",
                table: "TripParticipants");

            migrationBuilder.DropIndex(
                name: "IX_TripParticipants_UserId",
                table: "TripParticipants");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DescriptionMarkdown",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "TripParticipants");

            migrationBuilder.DropColumn(
                name: "IsAnonymous",
                table: "TripParticipants");

            migrationBuilder.DropColumn(
                name: "ParticipantId",
                table: "TripParticipants");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "TripParticipants",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
