using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtoCommerce.Punchout.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddPunchoutOrderLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cxml",
                table: "PunchoutOrderRequest",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderVersion",
                table: "PunchoutOrderRequest",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousPayloadId",
                table: "PunchoutOrderRequest",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Items",
                table: "PunchoutOrderMessage",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PunchoutOrderLine",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ConfigurationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LineItemId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PurchaseOrderNumber = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LineNumber = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OrderRequestId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
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
                name: "Cxml",
                table: "PunchoutOrderRequest");

            migrationBuilder.DropColumn(
                name: "OrderVersion",
                table: "PunchoutOrderRequest");

            migrationBuilder.DropColumn(
                name: "PreviousPayloadId",
                table: "PunchoutOrderRequest");

            migrationBuilder.DropColumn(
                name: "Items",
                table: "PunchoutOrderMessage");
        }
    }
}
