using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantAndDriverFlows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CurrentLat",
                table: "Shipments",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CurrentLng",
                table: "Shipments",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LocationUpdatedAt",
                table: "Shipments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DeliveryDate",
                table: "ShipmentRequests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "DeliveryTimeFrom",
                table: "ShipmentRequests",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "DeliveryTimeTo",
                table: "ShipmentRequests",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DropoffArea",
                table: "ShipmentRequests",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

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

            migrationBuilder.AddColumn<int>(
                name: "ParcelCount",
                table: "ShipmentRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ParcelSize",
                table: "ShipmentRequests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParcelType",
                table: "ShipmentRequests",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "ShipmentRequests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PickupArea",
                table: "ShipmentRequests",
                type: "character varying(256)",
                maxLength: 256,
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

            migrationBuilder.AddColumn<string>(
                name: "RequestNumber",
                table: "ShipmentRequests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                table: "ShipmentRequests",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SenderPhone",
                table: "ShipmentRequests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "MerchantProfiles",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentRequests_MerchantProfileId",
                table: "ShipmentRequests",
                column: "MerchantProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentRequests_RequestNumber",
                table: "ShipmentRequests",
                column: "RequestNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShipmentRequests_MerchantProfileId",
                table: "ShipmentRequests");

            migrationBuilder.DropIndex(
                name: "IX_ShipmentRequests_RequestNumber",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "CurrentLat",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "CurrentLng",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "LocationUpdatedAt",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "DeliveryDate",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "DeliveryTimeFrom",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "DeliveryTimeTo",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "DropoffArea",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "DropoffLat",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "DropoffLng",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "ParcelCount",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "ParcelSize",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "ParcelType",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "PickupArea",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "PickupLat",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "PickupLng",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "RequestNumber",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "SenderName",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "SenderPhone",
                table: "ShipmentRequests");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "MerchantProfiles");
        }
    }
}
