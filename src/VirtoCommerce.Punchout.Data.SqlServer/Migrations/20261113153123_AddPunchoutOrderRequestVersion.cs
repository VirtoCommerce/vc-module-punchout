using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.Punchout.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPunchoutOrderRequestVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cxml",
                table: "PunchoutOrderRequest",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderVersion",
                table: "PunchoutOrderRequest",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousPayloadId",
                table: "PunchoutOrderRequest",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cxml",
                table: "PunchoutOrderRequest");

            migrationBuilder.DropColumn(
                name: "OrderVersion",
                table: "PunchoutOrderRequest");

            migrationBuilder.DropColumn(
                name: "PreviousPayloadId",
                table: "PunchoutOrderRequest");
        }
    }
}
