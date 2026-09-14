using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.Punchout.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPunchoutSessionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuyerCookie",
                table: "PunchoutSession",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuyerDomain",
                table: "PunchoutSession",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuyerIdentity",
                table: "PunchoutSession",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CartId",
                table: "PunchoutSession",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpirationDate",
                table: "PunchoutSession",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrationId",
                table: "PunchoutSession",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnUrl",
                table: "PunchoutSession",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionToken",
                table: "PunchoutSession",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StartPage",
                table: "PunchoutSession",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PunchoutSession",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StoreId",
                table: "PunchoutSession",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutSession_SessionToken",
                table: "PunchoutSession",
                column: "SessionToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PunchoutSession_SessionToken",
                table: "PunchoutSession");

            migrationBuilder.DropColumn(
                name: "BuyerCookie",
                table: "PunchoutSession");

            migrationBuilder.DropColumn(
                name: "BuyerDomain",
                table: "PunchoutSession");

            migrationBuilder.DropColumn(
                name: "BuyerIdentity",
                table: "PunchoutSession");

            migrationBuilder.DropColumn(
                name: "CartId",
                table: "PunchoutSession");

            migrationBuilder.DropColumn(
                name: "ExpirationDate",
                table: "PunchoutSession");

            migrationBuilder.DropColumn(
                name: "IntegrationId",
                table: "PunchoutSession");

            migrationBuilder.DropColumn(
                name: "ReturnUrl",
                table: "PunchoutSession");

            migrationBuilder.DropColumn(
                name: "SessionToken",
                table: "PunchoutSession");

            migrationBuilder.DropColumn(
                name: "StartPage",
                table: "PunchoutSession");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PunchoutSession");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "PunchoutSession");
        }
    }
}
