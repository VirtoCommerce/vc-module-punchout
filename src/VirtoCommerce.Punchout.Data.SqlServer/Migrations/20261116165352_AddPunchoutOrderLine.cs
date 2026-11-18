using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.Punchout.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPunchoutOrderLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Items",
                table: "PunchoutOrderMessage",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PunchoutOrderLine",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ConfigurationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LineItemId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PurchaseOrderNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LineNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OrderRequestId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PunchoutOrderLine", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutOrderLine_ConfigurationId_LineItemId",
                table: "PunchoutOrderLine",
                columns: new[] { "ConfigurationId", "LineItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutOrderLine_ConfigurationId_PurchaseOrderNumber",
                table: "PunchoutOrderLine",
                columns: new[] { "ConfigurationId", "PurchaseOrderNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutOrderLine_ConfigurationId_SessionId_LineItemId",
                table: "PunchoutOrderLine",
                columns: new[] { "ConfigurationId", "SessionId", "LineItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PunchoutOrderLine");

            migrationBuilder.DropColumn(
                name: "Items",
                table: "PunchoutOrderMessage");
        }
    }
}
