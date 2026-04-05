using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropOffCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DropoffLat",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "DropoffLng",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "PickupLat",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "PickupLng",
                table: "ShipmentRequests");

            migrationBuilder.RenameColumn(
                name: "EstimatedWeight",
                table: "ShipmentRequests",
                newName: "EstimatedWeightTon");

            migrationBuilder.AddColumn<string>(
                name: "BodyType",
                table: "Trucks",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DropoffGovernorateId",
                table: "ShipmentRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DropoffRegionId",
                table: "ShipmentRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFragile",
                table: "ShipmentRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PickupGovernorateId",
                table: "ShipmentRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PickupRegionId",
                table: "ShipmentRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiverPhoneNumber",
                table: "ShipmentRequests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredTruckBodyType",
                table: "ShipmentRequests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpirationAt",
                table: "ShipmentOffers",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateTable(
                name: "Governorates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NameAr = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Governorates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Regions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GovernorateId = table.Column<Guid>(type: "uuid", nullable: false),
                    NameAr = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Regions_Governorates_GovernorateId",
                        column: x => x.GovernorateId,
                        principalTable: "Governorates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentRequests_DropoffGovernorateId",
                table: "ShipmentRequests",
                column: "DropoffGovernorateId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentRequests_DropoffRegionId",
                table: "ShipmentRequests",
                column: "DropoffRegionId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentRequests_PickupGovernorateId",
                table: "ShipmentRequests",
                column: "PickupGovernorateId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentRequests_PickupRegionId",
                table: "ShipmentRequests",
                column: "PickupRegionId");

            migrationBuilder.CreateIndex(
                name: "IX_Regions_GovernorateId",
                table: "Regions",
                column: "GovernorateId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShipmentRequests_Governorates_DropoffGovernorateId",
                table: "ShipmentRequests",
                column: "DropoffGovernorateId",
                principalTable: "Governorates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShipmentRequests_Governorates_PickupGovernorateId",
                table: "ShipmentRequests",
                column: "PickupGovernorateId",
                principalTable: "Governorates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShipmentRequests_Regions_DropoffRegionId",
                table: "ShipmentRequests",
                column: "DropoffRegionId",
                principalTable: "Regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShipmentRequests_Regions_PickupRegionId",
                table: "ShipmentRequests",
                column: "PickupRegionId",
                principalTable: "Regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShipmentRequests_Governorates_DropoffGovernorateId",
                table: "ShipmentRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ShipmentRequests_Governorates_PickupGovernorateId",
                table: "ShipmentRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ShipmentRequests_Regions_DropoffRegionId",
                table: "ShipmentRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ShipmentRequests_Regions_PickupRegionId",
                table: "ShipmentRequests");

            migrationBuilder.DropTable(
                name: "Regions");

            migrationBuilder.DropTable(
                name: "Governorates");

            migrationBuilder.DropIndex(
                name: "IX_ShipmentRequests_DropoffGovernorateId",
                table: "ShipmentRequests");

            migrationBuilder.DropIndex(
                name: "IX_ShipmentRequests_DropoffRegionId",
                table: "ShipmentRequests");

            migrationBuilder.DropIndex(
                name: "IX_ShipmentRequests_PickupGovernorateId",
                table: "ShipmentRequests");

            migrationBuilder.DropIndex(
                name: "IX_ShipmentRequests_PickupRegionId",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "BodyType",
                table: "Trucks");

            migrationBuilder.DropColumn(
                name: "DropoffGovernorateId",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "DropoffRegionId",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "IsFragile",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "PickupGovernorateId",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "PickupRegionId",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "ReceiverPhoneNumber",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "RequiredTruckBodyType",
                table: "ShipmentRequests");

            migrationBuilder.RenameColumn(
                name: "EstimatedWeightTon",
                table: "ShipmentRequests",
                newName: "EstimatedWeight");

            migrationBuilder.AddColumn<double>(
                name: "DropoffLat",
                table: "ShipmentRequests",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DropoffLng",
                table: "ShipmentRequests",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PickupLat",
                table: "ShipmentRequests",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PickupLng",
                table: "ShipmentRequests",
                type: "double precision",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpirationAt",
                table: "ShipmentOffers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
