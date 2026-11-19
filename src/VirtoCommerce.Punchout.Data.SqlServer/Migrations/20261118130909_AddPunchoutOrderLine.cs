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

            migrationBuilder.CreateTable(
                name: "PunchoutRequisitionItem",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OrderMessageId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LineItemId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CatalogId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CategoryId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Sku = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(1028)", maxLength: 1028, nullable: true),
                    ProductType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProductOuterId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PriceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsGift = table.Column<bool>(type: "bit", nullable: false),
                    FulfillmentCenterId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FulfillmentCenterName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    WeightUnit = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MeasureUnit = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Height = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Length = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Width = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ListPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SalePrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PlacedPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TaxType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TaxPercentRate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PunchoutRequisitionItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PunchoutRequisitionItem_PunchoutOrderMessage_OrderMessageId",
                        column: x => x.OrderMessageId,
                        principalTable: "PunchoutOrderMessage",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutRequisitionItem_LineItemId",
                table: "PunchoutRequisitionItem",
                column: "LineItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PunchoutRequisitionItem_OrderMessageId_LineItemId",
                table: "PunchoutRequisitionItem",
                columns: new[] { "OrderMessageId", "LineItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PunchoutOrderLine");

            migrationBuilder.DropTable(
                name: "PunchoutRequisitionItem");

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
