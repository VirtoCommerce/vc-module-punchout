using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.Punchout.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddPunchoutSessionOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrganizationId",
                table: "PunchoutSession",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserMappingId",
                table: "PunchoutSession",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutSession_OrganizationId",
                table: "PunchoutSession",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PunchoutSession_OrganizationId",
                table: "PunchoutSession");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "PunchoutSession");

            migrationBuilder.DropColumn(
                name: "UserMappingId",
                table: "PunchoutSession");
        }
    }
}
