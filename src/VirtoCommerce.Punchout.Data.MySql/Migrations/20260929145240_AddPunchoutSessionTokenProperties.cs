using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.Punchout.Data.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddPunchoutSessionTokenProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SessionToken",
                table: "PunchoutSession",
                newName: "SessionTokenHash");

            migrationBuilder.AddColumn<bool>(
                name: "IsSessionTokenRedeemed",
                table: "PunchoutSession",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "TokenExpirationDate",
                table: "PunchoutSession",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSessionTokenRedeemed",
                table: "PunchoutSession");

            migrationBuilder.DropColumn(
                name: "TokenExpirationDate",
                table: "PunchoutSession");

            migrationBuilder.RenameColumn(
                name: "SessionTokenHash",
                table: "PunchoutSession",
                newName: "SessionToken");
        }
    }
}
