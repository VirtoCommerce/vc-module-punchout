using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.Punchout.Data.MySql.Migrations
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
                type: "varchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(128)",
                oldMaxLength: 128)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "OrganizationId",
                table: "PunchoutUserMapping",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "OrganizationName",
                table: "PunchoutUserMapping",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

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

            migrationBuilder.UpdateData(
                table: "PunchoutUserMapping",
                keyColumn: "UserId",
                keyValue: null,
                column: "UserId",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "PunchoutUserMapping",
                type: "varchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(128)",
                oldMaxLength: 128,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutUserMapping_ExternalId",
                table: "PunchoutUserMapping",
                column: "ExternalId",
                unique: true);
        }
    }
}
