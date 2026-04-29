using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverAcceptanceFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "Shipments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "Shipments");
        }
    }
}
