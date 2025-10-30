using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripExpenseApi.Migrations
{
    /// <inheritdoc />
    public partial class TripParticipantsEtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InviteToken",
                table: "Trips",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "InviteTokenExpiry",
                table: "Trips",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInviteLinkActive",
                table: "Trips",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InviteToken",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "InviteTokenExpiry",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "IsInviteLinkActive",
                table: "Trips");
        }
    }
}
