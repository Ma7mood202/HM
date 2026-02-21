using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTruckAccountProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "TruckAccounts",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "TruckAccounts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "TruckAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NationalIdBackImageUrl",
                table: "TruckAccounts",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalIdFrontImageUrl",
                table: "TruckAccounts",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "TruckAccounts");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "TruckAccounts");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "TruckAccounts");

            migrationBuilder.DropColumn(
                name: "NationalIdBackImageUrl",
                table: "TruckAccounts");

            migrationBuilder.DropColumn(
                name: "NationalIdFrontImageUrl",
                table: "TruckAccounts");
        }
    }
}
