using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.Punchout.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCartIdIntegrationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CartId",
                table: "PunchoutSession");

            migrationBuilder.DropColumn(
                name: "IntegrationId",
                table: "PunchoutSession");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CartId",
                table: "PunchoutSession",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrationId",
                table: "PunchoutSession",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }
    }
}
