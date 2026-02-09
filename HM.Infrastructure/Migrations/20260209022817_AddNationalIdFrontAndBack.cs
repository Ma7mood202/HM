using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNationalIdFrontAndBack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "TruckAccounts");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "MerchantProfiles");

            migrationBuilder.RenameColumn(
                name: "NationalIdImageUrl",
                table: "DriverProfiles",
                newName: "NationalIdFrontImageUrl");

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "DriverProfiles",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalIdBackImageUrl",
                table: "DriverProfiles",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "NationalIdBackImageUrl",
                table: "DriverProfiles");

            migrationBuilder.RenameColumn(
                name: "NationalIdFrontImageUrl",
                table: "DriverProfiles",
                newName: "NationalIdImageUrl");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "TruckAccounts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "MerchantProfiles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }
    }
}
