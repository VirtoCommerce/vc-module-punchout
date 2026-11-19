using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.Punchout.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddPunchoutUserMappingOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PunchoutUserMapping_ExternalId",
                table: "PunchoutUserMapping");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "PunchoutUserMapping",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AddColumn<string>(
                name: "OrganizationId",
                table: "PunchoutUserMapping",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrganizationName",
                table: "PunchoutUserMapping",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutUserMapping_ExternalId",
                table: "PunchoutUserMapping",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutUserMapping_OrganizationId",
                table: "PunchoutUserMapping",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PunchoutUserMapping_ExternalId",
                table: "PunchoutUserMapping");

            migrationBuilder.DropIndex(
                name: "IX_PunchoutUserMapping_OrganizationId",
                table: "PunchoutUserMapping");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "PunchoutUserMapping");

            migrationBuilder.DropColumn(
                name: "OrganizationName",
                table: "PunchoutUserMapping");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "PunchoutUserMapping",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutUserMapping_ExternalId",
                table: "PunchoutUserMapping",
                column: "ExternalId",
                unique: true);
        }
    }
}
